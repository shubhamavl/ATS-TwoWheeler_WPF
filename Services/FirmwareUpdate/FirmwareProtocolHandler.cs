using System;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services.FirmwareUpdate
{
    /// <summary>
    /// Handles bootloader protocol commands (Ping, Begin, End)
    /// </summary>
    public class FirmwareProtocolHandler : IFirmwareProtocolHandler
    {
        private readonly ICANService _canService;
        private readonly CANBootloaderService _bootloaderService;
        private readonly ProductionLogger _logger = ProductionLogger.Instance;

        // Timeout constants
        private const int PING_TIMEOUT_MS = 2000;
        private const int BEGIN_TIMEOUT_MS = 2000;
        private const int END_TIMEOUT_MS = 10000;
        private const int ERASE_TIMEOUT_MS = 10000;

        // Wait sources for async response handling
        private TaskCompletionSource<bool>? _pingWaitSource;
        private TaskCompletionSource<BootloaderStatus>? _beginWaitSource;
        private TaskCompletionSource<BootloaderStatus>? _endWaitSource;
        private System.Collections.Concurrent.ConcurrentQueue<BootloaderStatus>? _beginResponseQueue;

        public FirmwareProtocolHandler(ICANService canService, CANBootloaderService bootloaderService)
        {
            _canService = canService;
            _bootloaderService = bootloaderService;
        }

        /// <summary>
        /// Send ping and wait for READY response with retry
        /// </summary>
        public async Task<bool> PingWithRetryAsync(int maxAttempts, int retryDelayMs, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    _logger.LogInfo($"Retrying ping (attempt {attempt + 1}/{maxAttempts})...", "ProtocolHandler");
                    await Task.Delay(retryDelayMs, cancellationToken);
                }

                _pingWaitSource = new TaskCompletionSource<bool>();

                if (!_bootloaderService.SendPing())
                {
                    _logger.LogError("Failed to send ping command", "ProtocolHandler");
                    _pingWaitSource = null;
                    continue;
                }

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(PING_TIMEOUT_MS);

                    try
                    {
                        await _pingWaitSource.Task.WaitAsync(timeoutCts.Token);
                        _logger.LogInfo("Ping response received successfully", "ProtocolHandler");
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning($"Ping timeout on attempt {attempt + 1}/{maxAttempts}", "ProtocolHandler");
                        _pingWaitSource = null;
                    }
                }
            }

            _logger.LogError("Ping failed after all retry attempts", "ProtocolHandler");
            return false;
        }

        /// <summary>
        /// Send BEGIN command and wait for InProgress then Success response
        /// </summary>
        public async Task<bool> BeginAsync(int size, CancellationToken cancellationToken)
        {
            _logger.LogInfo($"Sending Begin command with firmware size: {size} bytes", "ProtocolHandler");

            byte[] payload = BitConverter.GetBytes(size);
            if (!_canService.SendMessage(BootloaderProtocol.CanIdBootBegin, payload))
            {
                _logger.LogError("Failed to send Begin command", "ProtocolHandler");
                return false;
            }

            _beginResponseQueue = new System.Collections.Concurrent.ConcurrentQueue<BootloaderStatus>();
            _beginWaitSource = new TaskCompletionSource<BootloaderStatus>();

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(ERASE_TIMEOUT_MS);

                try
                {
                    // Wait for FIRST response (should be InProgress)
                    await _beginWaitSource.Task.WaitAsync(timeoutCts.Token);

                    if (!_beginResponseQueue.TryDequeue(out var firstStatus))
                    {
                        _logger.LogError("Begin response queue is empty", "ProtocolHandler");
                        return false;
                    }

                    if (firstStatus != BootloaderStatus.InProgress)
                    {
                        _logger.LogError($"Begin command rejected: {BootloaderProtocol.DescribeStatus(firstStatus)}", "ProtocolHandler");
                        return false;
                    }

                    _logger.LogInfo("Begin accepted. Erasing Flash...", "ProtocolHandler");

                    // Wait for SECOND response (should be Success)
                    _beginWaitSource = new TaskCompletionSource<BootloaderStatus>();

                    if (_beginResponseQueue.IsEmpty)
                    {
                        await _beginWaitSource.Task.WaitAsync(timeoutCts.Token);
                    }

                    if (!_beginResponseQueue.TryDequeue(out var secondStatus))
                    {
                        _logger.LogError("Expected SUCCESS response but queue is empty", "ProtocolHandler");
                        return false;
                    }

                    if (secondStatus == BootloaderStatus.Success)
                    {
                        _logger.LogInfo("Flash Erase Complete. Ready for data transfer.", "ProtocolHandler");
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"Flash Erase failed: {BootloaderProtocol.DescribeStatus(secondStatus)}", "ProtocolHandler");
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Timeout waiting for Begin responses ({ERASE_TIMEOUT_MS}ms)", "ProtocolHandler");
                    return false;
                }
                finally
                {
                    _beginResponseQueue = null;
                }
            }
        }

        /// <summary>
        /// Send END command and wait for SUCCESS response
        /// </summary>
        public async Task<bool> EndAsync(uint crc, CancellationToken cancellationToken)
        {
            _logger.LogInfo($"Sending End command with CRC: 0x{crc:X8}", "ProtocolHandler");

            byte[] payload = BitConverter.GetBytes(crc);
            if (!_canService.SendMessage(BootloaderProtocol.CanIdBootEnd, payload))
            {
                _logger.LogError("Failed to send End command", "ProtocolHandler");
                return false;
            }

            _endWaitSource = new TaskCompletionSource<BootloaderStatus>();

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(END_TIMEOUT_MS);

                try
                {
                    var receivedStatus = await _endWaitSource.Task.WaitAsync(timeoutCts.Token);

                    if (receivedStatus == BootloaderStatus.Success)
                    {
                        _logger.LogInfo("End command successful", "ProtocolHandler");
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"End command failed: {BootloaderProtocol.DescribeStatus(receivedStatus)}", "ProtocolHandler");
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Timeout waiting for End response ({END_TIMEOUT_MS}ms)", "ProtocolHandler");
                    return false;
                }
            }
        }

        /// <summary>
        /// Handle ping response from bootloader
        /// </summary>
        public void OnPingResponse()
        {
            _pingWaitSource?.TrySetResult(true);
        }

        /// <summary>
        /// Handle begin response from bootloader
        /// </summary>
        public void OnBeginResponse(BootloaderStatus status)
        {
            if (_beginResponseQueue != null)
            {
                _beginResponseQueue.Enqueue(status);
                _beginWaitSource?.TrySetResult(status);
            }
            else
            {
                _beginWaitSource?.TrySetResult(status);
            }
        }

        /// <summary>
        /// Handle end response from bootloader
        /// </summary>
        public void OnEndResponse(BootloaderStatus status)
        {
            _endWaitSource?.TrySetResult(status);
        }

        /// <summary>
        /// Clean up wait sources
        /// </summary>
        public void Cleanup()
        {
            _pingWaitSource = null;
            _beginWaitSource = null;
            _endWaitSource = null;
            _beginResponseQueue = null;
        }
    }
}
