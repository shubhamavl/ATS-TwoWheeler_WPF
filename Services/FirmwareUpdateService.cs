using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public sealed class FirmwareUpdateService
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

        private readonly CANService _canService;
        private readonly ProductionLogger _logger = ProductionLogger.Instance;
        private BootloaderDiagnosticsService? _diagnosticsService;
        
        // Update phase tracking
        private enum UpdatePhase { None, Ping, Begin, Transfer, End }
        private UpdatePhase _currentPhase = UpdatePhase.None;
        
        // Transfer error tracking
        private BootloaderStatus? _transferError = null;
        
        // Retry logic tracking
        private byte? _retryRequestedSequence = null;
        
        // Firmware info for progress tracking
        private int _firmwareLength = 0;
        
        // Status response waiting mechanism
        private TaskCompletionSource<bool>? _pingWaitSource;
        private TaskCompletionSource<BootloaderStatus>? _beginWaitSource;
        private TaskCompletionSource<BootloaderStatus>? _endWaitSource;
        
        // Event handlers
        private EventHandler<BootPingResponseEventArgs>? _pingHandler;
        private EventHandler<BootBeginResponseEventArgs>? _beginHandler;
        private EventHandler<BootEndResponseEventArgs>? _endHandler;
        private EventHandler<BootErrorEventArgs>? _errorHandler;
        private EventHandler<BootProgressEventArgs>? _progressHandler;

        public FirmwareUpdateService(CANService canService)
        {
            _canService = canService;
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
                throw new FileNotFoundException("Firmware binary not found", binPath);

            byte[] fullFirmware = await File.ReadAllBytesAsync(binPath, cancellationToken).ConfigureAwait(false);
            
            // Skip first 8KB (bootloader) - extract only application portion
            const int BootloaderSize = 0x2000; // 8KB
            if (fullFirmware.Length < BootloaderSize)
            {
                _logger.LogError($"Firmware file too small: {fullFirmware.Length} bytes (expected at least {BootloaderSize} bytes)", "FWUpdater");
                return false;
            }
            
            // Extract application portion (skip bootloader)
            byte[] firmware = new byte[fullFirmware.Length - BootloaderSize];
            Array.Copy(fullFirmware, BootloaderSize, firmware, 0, firmware.Length);
            
            // Validate firmware size (max 120KB for Bank A)
            if (firmware.Length > MAX_FIRMWARE_SIZE)
            {
                _logger.LogError($"Firmware too large: {firmware.Length} bytes (max {MAX_FIRMWARE_SIZE} bytes)", "FWUpdater");
                return false;
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
            
            // Subscribe to bootloader response events for response waiting
            _pingHandler = (sender, e) => OnPingResponseReceived();
            _beginHandler = (sender, e) => OnBeginResponseReceived(e);
            _endHandler = (sender, e) => OnEndResponseReceived(e);
            _errorHandler = (sender, e) => OnErrorReceived(e);
            _progressHandler = (sender, e) => OnProgressReceived(e, progress);
            
            _canService.BootPingResponseReceived += _pingHandler;
            _canService.BootBeginResponseReceived += _beginHandler;
            _canService.BootEndResponseReceived += _endHandler;
            _canService.BootErrorReceived += _errorHandler;
            _canService.BootProgressReceived += _progressHandler;
            
            try
            {

                // Enter bootloader (no data bytes needed)
                if (!_canService.RequestEnterBootloader())
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
                // Retry up to 3 times in case STM32 boots quickly and we miss the initial response
                _currentPhase = UpdatePhase.Ping;
                const int PING_RETRY_ATTEMPTS = 3;
                const int PING_RETRY_DELAY_MS = 500;
                bool pingSuccess = false;
                
                for (int pingAttempt = 0; pingAttempt < PING_RETRY_ATTEMPTS; pingAttempt++)
                {
                    if (pingAttempt > 0)
                    {
                        _logger.LogInfo($"Retrying ping (attempt {pingAttempt + 1}/{PING_RETRY_ATTEMPTS})...", "FWUpdater");
                        await Task.Delay(PING_RETRY_DELAY_MS, cancellationToken).ConfigureAwait(false);
                    }
                    
                    // Create new wait source for this attempt
                    _pingWaitSource = new TaskCompletionSource<bool>();
                    
                    // Send ping
                    if (!SendPing())
                    {
                        _logger.LogError("Failed to send ping command", "FWUpdater");
                        _pingWaitSource = null;
                        continue; // Try next attempt
                    }
                    
                    // Wait for response with timeout
                    using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        timeoutCts.CancelAfter(PING_TIMEOUT_MS);
                        
                        try
                        {
                            await _pingWaitSource.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                            pingSuccess = true;
                            _logger.LogInfo("Ping response received successfully", "FWUpdater");
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning($"Ping timeout on attempt {pingAttempt + 1}/{PING_RETRY_ATTEMPTS} (timeout: {PING_TIMEOUT_MS}ms)", "FWUpdater");
                            // Clear wait source so old responses don't interfere
                            _pingWaitSource = null;
                            // Continue to next retry attempt
                        }
                    }
                }
                
                if (!pingSuccess)
                {
                    _logger.LogError("Bootloader ping failed or timeout after all retry attempts", "FWUpdater");
                    return false;
                }

                // Send application size (not full file size) and wait for IN_PROGRESS response
                _currentPhase = UpdatePhase.Begin;
                if (!await SendBeginCommandWithResponse(firmware.Length, BEGIN_TIMEOUT_MS, cancellationToken))
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
                if (!await SendEndCommandWithResponse(finalCrc, END_TIMEOUT_MS, cancellationToken))
                {
                    _logger.LogError("Bootloader end command failed or timeout", "FWUpdater");
                    return false;
                }

                _logger.LogInfo("Firmware update completed successfully. Sending reset command...", "FWUpdater");
                
                // Send reset command to boot new firmware
                if (!_canService.RequestReset())
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
                    _canService.BootPingResponseReceived -= _pingHandler;
                    _pingHandler = null;
                }
                if (_beginHandler != null)
                {
                    _canService.BootBeginResponseReceived -= _beginHandler;
                    _beginHandler = null;
                }
                if (_endHandler != null)
                {
                    _canService.BootEndResponseReceived -= _endHandler;
                    _endHandler = null;
                }
                if (_errorHandler != null)
                {
                    _canService.BootErrorReceived -= _errorHandler;
                    _errorHandler = null;
                }
                if (_progressHandler != null)
                {
                    _canService.BootProgressReceived -= _progressHandler;
                    _progressHandler = null;
                }
                _pingWaitSource = null;
                _beginWaitSource = null;
                _endWaitSource = null;
                _currentPhase = UpdatePhase.None;
                _transferError = null;
                _retryRequestedSequence = null;
                _firmwareLength = 0;
            }
        }
        
        /// <summary>
        /// Handle ping response (READY)
        /// </summary>
        private void OnPingResponseReceived()
        {
            // Only set result if we're in ping phase and wait source exists
            if (_currentPhase == UpdatePhase.Ping && _pingWaitSource != null)
            {
                _pingWaitSource.TrySetResult(true);
            }
        }
        
        /// <summary>
        /// Handle begin response (IN_PROGRESS or FAILED)
        /// </summary>
        private void OnBeginResponseReceived(BootBeginResponseEventArgs e)
        {
            _beginWaitSource?.TrySetResult(e.Status);
        }
        
        /// <summary>
        /// Handle end response (SUCCESS or FAILED)
        /// </summary>
        private void OnEndResponseReceived(BootEndResponseEventArgs e)
        {
            _endWaitSource?.TrySetResult(e.Status);
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
            // Route error to appropriate wait source based on current phase
            switch (_currentPhase)
            {
                case UpdatePhase.Begin:
                    // Error during BEGIN phase - set begin wait source
                    _beginWaitSource?.TrySetResult(e.ErrorCode);
                    break;
                    
                case UpdatePhase.End:
                    // Error during END phase - set end wait source
                    _endWaitSource?.TrySetResult(e.ErrorCode);
                    break;
                    
                case UpdatePhase.Transfer:
                    // Error during transfer phase - track for data loop to check
                    if (e.ErrorCode == BootloaderStatus.FailedChecksum && e.AdditionalData != 0)
                    {
                        // Retry request: STM32 wants us to retransmit from this sequence number
                        _logger.LogWarning($"Bootloader requested retry from sequence {e.AdditionalData} during transfer", "FWUpdater");
                        _retryRequestedSequence = (byte)e.AdditionalData;
                        // Clear transfer error since this is a retry request, not a fatal error
                        _transferError = null;
                    }
                    else
                    {
                        _logger.LogError($"Transfer error received: {BootloaderProtocol.DescribeStatus(e.ErrorCode)}", "FWUpdater");
                        // Set transfer error to abort update
                        _transferError = e.ErrorCode;
                    }
                    break;
                    
                default:
                    // Error in other phases - log but don't set wait source
                    _logger.LogWarning($"Error received in phase {_currentPhase}: {BootloaderProtocol.DescribeStatus(e.ErrorCode)}", "FWUpdater");
                    break;
            }
        }
        
        /// <summary>
        /// Send ping and wait for READY response
        /// </summary>
        private async Task<bool> SendPingWithResponse(int timeoutMs, CancellationToken cancellationToken)
        {
            if (!SendPing())
            {
                return false;
            }
            
            _pingWaitSource = new TaskCompletionSource<bool>();
            
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeoutMs);
                
                try
                {
                    await _pingWaitSource.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Timeout waiting for ping response (timeout: {timeoutMs}ms)", "FWUpdater");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Send BEGIN command and wait for IN_PROGRESS response
        /// </summary>
        private async Task<bool> SendBeginCommandWithResponse(int size, int timeoutMs, CancellationToken cancellationToken)
        {
            _logger.LogInfo($"Sending Begin command with firmware size: {size} bytes", "FWUpdater");
            
            if (!SendBeginCommand(size))
            {
                _logger.LogError("Failed to send Begin command - CAN send failed", "FWUpdater");
                return false;
            }
            
            _beginWaitSource = new TaskCompletionSource<BootloaderStatus>();
            
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeoutMs);
                
                try
                {
                    var receivedStatus = await _beginWaitSource.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    
                    if (receivedStatus == BootloaderStatus.InProgress)
                    {
                        _logger.LogInfo("Begin command accepted - update in progress", "FWUpdater");
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"Begin command rejected with status: {BootloaderProtocol.DescribeStatus(receivedStatus)}", "FWUpdater");
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Timeout waiting for begin response (timeout: {timeoutMs}ms) - STM32 may not be in bootloader mode or CAN communication issue", "FWUpdater");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Send END command and wait for SUCCESS response
        /// </summary>
        private async Task<bool> SendEndCommandWithResponse(uint crc, int timeoutMs, CancellationToken cancellationToken)
        {
            if (!SendEndCommand(crc))
            {
                return false;
            }
            
            _endWaitSource = new TaskCompletionSource<BootloaderStatus>();
            
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(timeoutMs);
                
                try
                {
                    var receivedStatus = await _endWaitSource.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                    
                    if (receivedStatus == BootloaderStatus.Success)
                    {
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"Bootloader end command returned status: {BootloaderProtocol.DescribeStatus(receivedStatus)}", "FWUpdater");
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Timeout waiting for end response (timeout: {timeoutMs}ms)", "FWUpdater");
                    return false;
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
            bool result = _canService.SendMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>());
            
            if (result)
            {
                _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>(), true);
            }
            else
            {
                _logger.LogError($"Failed to send Ping command (CAN ID: 0x{BootloaderProtocol.CanIdBootPing:X3})", "FWUpdater");
            }
            
            return result;
        }

        private bool SendBeginCommand(int size)
        {
            // Begin command: 4 bytes (firmware size, little-endian uint32_t)
            byte[] payload = BitConverter.GetBytes(size);
            
            // Send message first, then capture in diagnostics only if successful
            bool result = _canService.SendMessage(BootloaderProtocol.CanIdBootBegin, payload);
            
            if (result)
            {
                _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootBegin, payload, true);
            }
            else
            {
                _logger.LogError($"Failed to send Begin command (CAN ID: 0x{BootloaderProtocol.CanIdBootBegin:X3})", "FWUpdater");
            }
            
            return result;
        }

        private bool SendEndCommand(uint crc)
        {
            // End command: 4 bytes (CRC32, little-endian uint32_t)
            byte[] payload = BitConverter.GetBytes(crc);
            _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootEnd, payload, true);
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

