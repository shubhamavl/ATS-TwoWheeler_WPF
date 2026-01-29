using System;
using System.Threading;
using System.Threading.Tasks;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    /// <summary>
    /// Interface for firmware update operations
    /// Enables testability and dependency injection
    /// </summary>
    public interface IFirmwareUpdateService
    {
        /// <summary>
        /// Perform firmware update from binary file
        /// </summary>
        /// <param name="binPath">Path to firmware binary file</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if update successful</returns>
        Task<bool> UpdateFirmwareAsync(
            string binPath,
            IProgress<FirmwareProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Set diagnostics service for message capture
        /// </summary>
        void SetDiagnosticsService(BootloaderDiagnosticsService? diagnosticsService);
    }

    /// <summary>
    /// Interface for bootloader state machine operations
    /// </summary>
    public interface IBootloaderStateMachine
    {
        /// <summary>
        /// Current process step
        /// </summary>
        ViewModels.Bootloader.BootloaderProcessStep CurrentStep { get; }

        /// <summary>
        /// Event fired when state changes
        /// </summary>
        event EventHandler<ViewModels.Bootloader.BootloaderProcessStep>? StepChanged;

        /// <summary>
        /// Transition to a new step with validation
        /// </summary>
        void TransitionTo(ViewModels.Bootloader.BootloaderProcessStep newStep);

        /// <summary>
        /// Force set state without validation
        /// </summary>
        void ForceSet(ViewModels.Bootloader.BootloaderProcessStep step);

        /// <summary>
        /// Reset to idle state
        /// </summary>
        void Reset();

        /// <summary>
        /// Get description of current step
        /// </summary>
        string GetStepDescription();
    }
}
