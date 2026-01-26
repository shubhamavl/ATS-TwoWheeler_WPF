using System;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Adapters;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.ViewModels.Bootloader;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    /// <summary>
    /// Interface for bootloader CAN communication operations.
    /// Manages low-level protocol commands and response events.
    /// </summary>
    public interface ICANBootloaderService
    {
        /// <summary>
        /// Fired when a ping response is received from the bootloader.
        /// </summary>
        event EventHandler<BootPingResponseEventArgs>? BootPingResponseReceived;

        /// <summary>
        /// Fired when a BEGIN command response is received (InProgress or Success/Error).
        /// </summary>
        event EventHandler<BootBeginResponseEventArgs>? BootBeginResponseReceived;

        /// <summary>
        /// Fired when an END command response is received.
        /// </summary>
        event EventHandler<BootEndResponseEventArgs>? BootEndResponseReceived;

        /// <summary>
        /// Fired when the bootloader sends an error message.
        /// </summary>
        event EventHandler<BootErrorEventArgs>? BootErrorReceived;

        /// <summary>
        /// Fired when a progress update is received during firmware transfer.
        /// </summary>
        event EventHandler<BootProgressEventArgs>? BootProgressReceived;

        /// <summary>
        /// Fired when bootloader information (version, banks) is received.
        /// </summary>
        event EventHandler<BootQueryResponseEventArgs>? BootQueryResponseReceived;

        /// <summary>
        /// Sends a request to the device to enter bootloader mode.
        /// </summary>
        /// <returns>True if the CAN message was sent successfully.</returns>
        bool RequestEnterBootloader();

        /// <summary>
        /// Sends a request to the bootloader to reset the device.
        /// </summary>
        /// <returns>True if the CAN message was sent successfully.</returns>
        bool RequestReset();

        /// <summary>
        /// Sends a query for bootloader version and bank status.
        /// </summary>
        /// <returns>True if the CAN message was sent successfully.</returns>
        bool QueryBootloaderInfo();

        /// <summary>
        /// Sends a ping to check if the bootloader is responsive.
        /// </summary>
        /// <returns>True if the CAN message was sent successfully.</returns>
        bool SendPing();

        /// <summary>
        /// Manually injects or processes a bootloader message.
        /// Useful for testing and diagnostics.
        /// </summary>
        /// <param name="canId">The CAN identifier.</param>
        /// <param name="data">The raw message data.</param>
        void ProcessBootloaderMessage(uint canId, byte[] data);
    }

    /// <summary>
    /// Interface for bootloader diagnostics operations.
    /// Tracks and exports communication logs for troubleshooting.
    /// </summary>
    public interface IBootloaderDiagnosticsService
    {
        /// <summary>
        /// Captures a bootloader message for diagnostics.
        /// </summary>
        /// <param name="canId">The CAN identifier.</param>
        /// <param name="data">The message data.</param>
        /// <param name="isSent">True if this was an outgoing message.</param>
        void CaptureMessage(uint canId, byte[] data, bool isSent);

        /// <summary>
        /// Generates a human-readable text report of all captured messages.
        /// </summary>
        /// <returns>A formatted log string.</returns>
        string ExportMessages();

        /// <summary>
        /// Clears all captured diagnostic messages.
        /// </summary>
        void ClearMessages();
    }

    /// <summary>
    /// Interface for firmware protocol handling.
    /// Manages the high-level sequence of commands (Ping -> Begin -> Transfer -> End).
    /// </summary>
    public interface IFirmwareProtocolHandler
    {
        /// <summary>
        /// Attempts to ping the bootloader with retries.
        /// </summary>
        /// <param name="maxRetries">Maximum number of attempts.</param>
        /// <param name="retryDelayMs">Delay between attempts in milliseconds.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Awaitable task with true if ping succeeded.</returns>
        Task<bool> PingWithRetryAsync(int maxRetries, int retryDelayMs, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a BEGIN command to prepare for firmware flash.
        /// </summary>
        /// <param name="firmwareSize">Total size of firmware in bytes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Awaitable task with true if flash preparation succeeded.</returns>
        Task<bool> BeginAsync(int firmwareSize, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an END command to finalize the firmware update.
        /// </summary>
        /// <param name="crc">The checksum of the flashed firmware.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Awaitable task with true if finalization succeeded.</returns>
        Task<bool> EndAsync(uint crc, CancellationToken cancellationToken);

        /// <summary>
        /// Internal handler for ping responses.
        /// </summary>
        void OnPingResponse();

        /// <summary>
        /// Internal handler for BEGIN command responses.
        /// </summary>
        /// <param name="status">The status code from the bootloader.</param>
        void OnBeginResponse(BootloaderStatus status);

        /// <summary>
        /// Internal handler for END command responses.
        /// </summary>
        /// <param name="status">The status code from the bootloader.</param>
        void OnEndResponse(BootloaderStatus status);

        /// <summary>
        /// Releases resources and resets wait states.
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Interface for logging abstraction.
    /// Allows swapping different logging providers.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Logs a debug message.</summary>
        void LogDebug(string message, string source = "");
        /// <summary>Logs an informational message.</summary>
        void LogInfo(string message, string source = "");
        /// <summary>Logs a warning message.</summary>
        void LogWarning(string message, string source = "");
        /// <summary>Logs an error message.</summary>
        void LogError(string message, string source = "");
        /// <summary>Logs an exception with a custom message.</summary>
        void LogError(Exception ex, string message, string source = "");
    }
}
