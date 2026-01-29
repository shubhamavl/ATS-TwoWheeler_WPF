using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Adapters;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Core.Exceptions;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Services.FirmwareUpdate;

namespace ATS_TwoWheeler_WPF.Services
{
    public sealed class FirmwareUpdateService : IFirmwareUpdateService
    {
        private const int MaxChunkSize = 7;  // Reduced from 8: Byte 0 = sequence number, Bytes 1-7 = data
        private const uint DataId = BootloaderProtocol.CanIdBootData;

        // Timeout constants
        private const int PING_TIMEOUT_MS = 2000;  // Reduced from 5000ms - bootloader responds quickly
        private const int BEGIN_TIMEOUT_MS = 2000;
        private const int END_TIMEOUT_MS = 10000;
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 50;  // Base delay for exponential backoff

        // Firmware size limits
        private const int MAX_FIRMWARE_SIZE = 0x1E000;  // 120KB (Bank A limit)
        private const int SEQUENCE_WRAP_SIZE = 256 * 7;  // 18,176 bytes per wrap (sequence wraps at 256)

        private readonly ICANService _canService;
        private readonly CANBootloaderService _bootloaderService;
        private readonly ProductionLogger _logger = ProductionLogger.Instance;
        private BootloaderDiagnosticsService? _diagnosticsService;

        private readonly FirmwareProtocolHandler _protocolHandler;
        private readonly FirmwareFlashState _flashState;

        // Update phase tracking
        private enum UpdatePhase { None, Ping, Begin, Transfer, End }
        private UpdatePhase _currentPhase = UpdatePhase.None;

        // Transfer error tracking
        private BootloaderStatus? _transferError = null;

        // Retry logic tracking
        private byte? _retryRequestedSequence = null;

        // Firmware info for progress tracking  
        private int _firmwareLength = 0;

        // Event handlers
        private EventHandler<BootPingResponseEventArgs>? _pingHandler;
        private EventHandler<BootBeginResponseEventArgs>? _beginHandler;
        private EventHandler<BootEndResponseEventArgs>? _endHandler;
        private EventHandler<BootErrorEventArgs>? _errorHandler;
        private EventHandler<BootProgressEventArgs>? _progressHandler;

        public FirmwareUpdateService(ICANService canService)
        {
            _canService = canService;
            _bootloaderService = new CANBootloaderService(canService);
            _protocolHandler = new FirmwareProtocolHandler(canService, _bootloaderService);
            _flashState = new FirmwareFlashState();
        }

        /// <summary>
        /// Set diagnostics service for message capture
        /// </summary>
        public void SetDiagnosticsService(BootloaderDiagnosticsService? diagnosticsService)
        {
            _diagnosticsService = diagnosticsService;
        }

        public async Task<bool> UpdateFirmwareAsync(string binPath, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(binPath))
            {
                throw new FirmwareValidationException(binPath, "File not found");
            }

            byte[] fullFirmware = await File.ReadAllBytesAsync(binPath, cancellationToken).ConfigureAwait(false);

            // In the current setup, we assume the provided .bin file is the application-only binary 
            // starting directly at APP_BANK_A_START (0x08008000). 
            // Previous logic was skipping 8KB (0x2000) which led to corrupted data being sent.
            byte[] firmware = fullFirmware;

            // Validate firmware size (max 120KB for Bank A)
            if (firmware.Length > MAX_FIRMWARE_SIZE)
            {
                _logger.LogError($"Firmware too large: {firmware.Length} bytes (max {MAX_FIRMWARE_SIZE} bytes)", "FWUpdater");
                throw new FirmwareSizeException(firmware.Length, MAX_FIRMWARE_SIZE);
            }

            // Warn if firmware will cause sequence number wrap-around
            if (firmware.Length > SEQUENCE_WRAP_SIZE)
            {
                int wraps = (firmware.Length + SEQUENCE_WRAP_SIZE - 1) / SEQUENCE_WRAP_SIZE;
                _logger.LogInfo($"Firmware size {firmware.Length} bytes will cause {wraps} sequence number wrap(s). STM32 handles this correctly.", "FWUpdater");
            }

            int totalChunks = (firmware.Length + (MaxChunkSize - 1)) / MaxChunkSize;
            _firmwareLength = firmware.Length;  // Store for progress handler

            _logger.LogInfo($"Firmware update start. Full size={fullFirmware.Length} bytes, Application size={firmware.Length} bytes, Chunks={totalChunks}", "FWUpdater");

            // Initialize flash state
            _flashState.Reset();
            _flashState.TotalBytes = firmware.Length;
            _flashState.TotalChunks = totalChunks;
            _flashState.StartTime = DateTime.Now;

            // Subscribe to bootloader response events
            _pingHandler = (sender, e) => _protocolHandler.OnPingResponse();
            _beginHandler = (sender, e) => _protocolHandler.OnBeginResponse(e.Status);
            _endHandler = (sender, e) => _protocolHandler.OnEndResponse(e.Status);
            _errorHandler = (sender, e) => OnErrorReceived(e);
            _progressHandler = (sender, e) => OnProgressReceived(e, progress);

            _bootloaderService.BootPingResponseReceived += _pingHandler;
            _bootloaderService.BootBeginResponseReceived += _beginHandler;
            _bootloaderService.BootEndResponseReceived += _endHandler;
            _bootloaderService.BootErrorReceived += _errorHandler;
            _bootloaderService.BootProgressReceived += _progressHandler;

            try
            {

                // Enter bootloader (no data bytes needed)
                if (!_bootloaderService.RequestEnterBootloader())
                {
                    _logger.LogError("Failed to request bootloader entry", "FWUpdater");
                    return false;
                }

                // Capture Enter message after successful send
                _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootEnter, Array.Empty<byte>(), true);

                // Wait for STM32 to reset and enter bootloader mode
                // STM32 reset + boot + CAN init + bootloader entry takes ~500-1000ms
                _logger.LogInfo("Waiting for STM32 to reset and enter bootloader mode...", "FWUpdater");
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                // Send ping and wait for READY response with retry mechanism
                _currentPhase = UpdatePhase.Ping;
                const int PING_RETRY_ATTEMPTS = 3;
                const int PING_RETRY_DELAY_MS = 500;

                bool pingSuccess = await _protocolHandler.PingWithRetryAsync(
                    PING_RETRY_ATTEMPTS, PING_RETRY_DELAY_MS, cancellationToken);

                if (!pingSuccess)
                {
                    _logger.LogError("Bootloader ping failed or timeout after all retry attempts", "FWUpdater");
                    return false;
                }

                // Send application size and wait for IN_PROGRESS then SUCCESS response
                _currentPhase = UpdatePhase.Begin;
                if (!await _protocolHandler.BeginAsync(firmware.Length, cancellationToken))
                {
                    _logger.LogError("Bootloader begin command failed or timeout", "FWUpdater");
                    return false;
                }

                uint runningCrc = 0xFFFFFFFFu;
                byte sequenceNumber = 0;  // Sequence number starts at 0, wraps at 256

                _currentPhase = UpdatePhase.Transfer;
                _transferError = null;  // Clear any previous transfer errors
                _retryRequestedSequence = null;  // Clear any previous retry requests

                // Data transfer loop with retry support
                int chunk = 0;
                while (chunk < totalChunks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check for retry request from STM32
                    if (_retryRequestedSequence.HasValue)
                    {
                        byte requestedSeq = _retryRequestedSequence.Value;
                        _logger.LogInfo($"Handling retry request: restarting from sequence {requestedSeq}", "FWUpdater");

                        // Calculate chunk index from sequence number
                        // Sequence number directly maps to chunk index (each chunk has one sequence number)
                        chunk = requestedSeq;
                        sequenceNumber = requestedSeq;

                        // Recalculate CRC up to the retry point
                        // We need to recalculate CRC for all data up to (but not including) the retry point
                        runningCrc = 0xFFFFFFFFu;
                        int bytesToRecalculate = chunk * MaxChunkSize;
                        for (int i = 0; i < bytesToRecalculate; i += MaxChunkSize)
                        {
                            int chunkRemaining = Math.Min(MaxChunkSize, firmware.Length - i);
                            byte[] chunkData = new byte[chunkRemaining];
                            Array.Copy(firmware, i, chunkData, 0, chunkRemaining);
                            runningCrc = UpdateCrc(runningCrc, chunkData);
                        }

                        // Clear retry request
                        _retryRequestedSequence = null;
                        _logger.LogInfo($"Retry: recalculated CRC for {bytesToRecalculate} bytes, resuming from chunk {chunk}", "FWUpdater");
                    }

                    // Check for transfer errors from STM32 (non-retry errors)
                    if (_transferError.HasValue)
                    {
                        _logger.LogError($"Transfer aborted due to error: {BootloaderProtocol.DescribeStatus(_transferError.Value)}", "FWUpdater");
                        return false;
                    }

                    // Check CAN connection before sending
                    if (!_canService.IsConnected)
                    {
                        _logger.LogError("CAN connection lost during update", "FWUpdater");
                        return false;
                    }

                    int offset = chunk * MaxChunkSize;
                    int remaining = Math.Min(MaxChunkSize, firmware.Length - offset);
                    byte[] data = new byte[remaining];
                    Array.Copy(firmware, offset, data, 0, remaining);

                    // Create frame with sequence number: Byte 0 = sequence, Bytes 1-7 = data
                    byte[] frame = new byte[8];
                    frame[0] = sequenceNumber;
                    Array.Copy(data, 0, frame, 1, remaining);
                    // Pad remaining bytes with 0xFF if needed
                    for (int i = remaining + 1; i < 8; i++)
                    {
                        frame[i] = 0xFF;
                    }

                    // Capture data frame for diagnostics
                    _diagnosticsService?.CaptureMessage(DataId, frame, true);

                    // Send with retry mechanism
                    if (!await SendMessageWithRetry(DataId, frame, cancellationToken))
                    {
                        _logger.LogError($"Failed to send chunk {chunk} (sequence {sequenceNumber}) after {MAX_RETRIES} retries", "FWUpdater");
                        return false;
                    }

                    // Update CRC only on data bytes (exclude sequence number)
                    runningCrc = UpdateCrc(runningCrc, data);
                    sequenceNumber = (byte)((sequenceNumber + 1) % 256);  // Explicit wrap-around

                    chunk++;  // Move to next chunk
                    progress?.Report(new FirmwareProgress(chunk, totalChunks));

                    await Task.Delay(2, cancellationToken).ConfigureAwait(false);

                    // Check for errors after delay (STM32 may have sent error response)
                    if (_transferError.HasValue)
                    {
                        _logger.LogError($"Transfer aborted due to error: {BootloaderProtocol.DescribeStatus(_transferError.Value)}", "FWUpdater");
                        return false;
                    }
                }

                uint finalCrc = runningCrc ^ 0xFFFFFFFFu;

                // Send END command and wait for SUCCESS response
                _currentPhase = UpdatePhase.End;
                if (!await _protocolHandler.EndAsync(finalCrc, cancellationToken))
                {
                    _logger.LogError("Bootloader end command failed or timeout", "FWUpdater");
                    return false;
                }

                _logger.LogInfo("Firmware update completed successfully. Sending reset command...", "FWUpdater");

                // Send reset command to boot new firmware
                if (!_bootloaderService.RequestReset())
                {
                    _logger.LogWarning("Failed to send reset command, but update was successful", "FWUpdater");
                    // Don't fail the update if reset command fails - update was successful
                }
                else
                {
                    _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootReset, Array.Empty<byte>(), true);
                    _logger.LogInfo("Reset command sent successfully. STM32 will boot from new firmware.", "FWUpdater");

                    // Small delay to allow STM32 to process reset command
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                // Unsubscribe from bootloader response events
                if (_pingHandler != null)
                {
                    _bootloaderService.BootPingResponseReceived -= _pingHandler;
                    _pingHandler = null;
                }
                if (_beginHandler != null)
                {
                    _bootloaderService.BootBeginResponseReceived -= _beginHandler;
                    _beginHandler = null;
                }
                if (_endHandler != null)
                {
                    _bootloaderService.BootEndResponseReceived -= _endHandler;
                    _endHandler = null;
                }
                if (_errorHandler != null)
                {
                    _bootloaderService.BootErrorReceived -= _errorHandler;
                    _errorHandler = null;
                }
                if (_progressHandler != null)
                {
                    _bootloaderService.BootProgressReceived -= _progressHandler;
                    _progressHandler = null;
                }

                // Clean up protocol handler
                _protocolHandler.Cleanup();
                _currentPhase = UpdatePhase.None;
                _transferError = null;
                _retryRequestedSequence = null;
                _firmwareLength = 0;
            }
        }

        /// <summary>
        /// Handle progress update from STM32
        /// </summary>
        private void OnProgressReceived(BootProgressEventArgs e, IProgress<FirmwareProgress>? progress)
        {
            // STM32 reports progress every 256 bytes
            // Use STM32-reported bytes to calculate more accurate progress
            if (progress != null && _currentPhase == UpdatePhase.Transfer && _firmwareLength > 0)
            {
                // Calculate chunks from bytes received (more accurate than our chunk count)
                int totalExpectedChunks = (_firmwareLength + (MaxChunkSize - 1)) / MaxChunkSize;
                int chunksReceived = (int)((e.BytesReceived + (MaxChunkSize - 1)) / MaxChunkSize);

                // Use STM32-reported progress, capped at expected chunks
                int calculatedChunks = Math.Min(chunksReceived, totalExpectedChunks);
                progress.Report(new FirmwareProgress(calculatedChunks, totalExpectedChunks));

                _logger.LogInfo($"Progress update from STM32: {e.Percent}% ({e.BytesReceived}/{_firmwareLength} bytes, {calculatedChunks}/{totalExpectedChunks} chunks)", "FWUpdater");
            }
        }

        /// <summary>
        /// Handle error response (retry requests, failures)
        /// </summary>
        private void OnErrorReceived(BootErrorEventArgs e)
        {
            _logger.LogError($"Bootloader error: {e.Message}", "FWUpdater");

            // Only handle transfer phase errors - protocol handler handles Begin/End errors
            if (_currentPhase == UpdatePhase.Transfer)
            {
                // Check for retry request: CanIdBootError is now used specifically for SEQ mismatch (0x51B)
                // New Payload format: [ExpectedSeq, ReceivedSeq]
                if (e.CanId == BootloaderProtocol.CanIdBootError && e.RawData != null && e.RawData.Length >= 2)
                {
                    byte requestedSeq = e.RawData[0]; // Expected sequence
                    _logger.LogWarning($"Bootloader requested retry from sequence {requestedSeq} due to mismatch", "FWUpdater");
                    _retryRequestedSequence = requestedSeq;
                    _transferError = null;
                }
                else
                {
                    // Any other CAN ID (Size, Write, Validation, Buffer) is a fatal transfer error
                    _transferError = BootloaderStatus.FailedFlash;
                }
            }
        }

        /// <summary>
        /// Send CAN message with retry mechanism (exponential backoff)
        /// </summary>
        private async Task<bool> SendMessageWithRetry(uint canId, byte[] data, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                // Check connection before each attempt
                if (!_canService.IsConnected)
                {
                    _logger.LogError("CAN connection lost during retry", "FWUpdater");
                    return false;
                }

                if (_canService.SendMessage(canId, data))
                {
                    return true;
                }

                // Exponential backoff: 50ms, 100ms, 200ms
                if (attempt < MAX_RETRIES - 1)
                {
                    int delay = RETRY_DELAY_MS * (1 << attempt);
                    _logger.LogWarning($"CAN send failed, retrying in {delay}ms (attempt {attempt + 1}/{MAX_RETRIES})", "FWUpdater");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            return false;
        }

        private bool SendPing()
        {
            // Ping command: no data bytes needed
            return _bootloaderService.SendPing();
        }

        private bool SendBeginCommand(int size)
        {
            // Begin command: 4 bytes (firmware size, little-endian uint32_t)
            byte[] payload = BitConverter.GetBytes(size);
            return _canService.SendMessage(BootloaderProtocol.CanIdBootBegin, payload);
        }

        private bool SendEndCommand(uint crc)
        {
            // End command: 4 bytes (CRC32, little-endian uint32_t)
            byte[] payload = BitConverter.GetBytes(crc);
            return _canService.SendMessage(BootloaderProtocol.CanIdBootEnd, payload);
        }


        // Note: PadData is no longer used - data frames now include sequence number
        // Frame format: [Sequence(1 byte)][Data(1-7 bytes)][Padding(0xFF if needed)]

        private static uint UpdateCrc(uint running, byte[] data)
        {
            const uint polynomial = 0x04C11DB7u;
            uint crc = running;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x00000001) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }
    }

    public readonly struct FirmwareProgress
    {
        public int ChunksSent { get; }
        public int TotalChunks { get; }

        public FirmwareProgress(int chunksSent, int totalChunks)
        {
            ChunksSent = chunksSent;
            TotalChunks = totalChunks;
        }

        public double Percentage => (double)ChunksSent / TotalChunks * 100.0;
    }
}

