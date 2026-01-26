using System;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.ViewModels.Bootloader
{
    /// <summary>
    /// Bootloader process states
    /// </summary>
    public enum BootloaderProcessStep
    {
        Idle = 0,
        EnterBootloader = 1,
        Ping = 2,
        Begin = 3,
        Transfer = 4,
        End = 5,
        Reset = 6,
        Complete = 7,
        Failed = 8
    }

    /// <summary>
    /// Manages bootloader process state transitions and validation
    /// </summary>
    public class BootloaderStateMachine : IBootloaderStateMachine
    {
        /// <summary>
        /// Current process step
        /// </summary>
        public BootloaderProcessStep CurrentStep { get; private set; } = BootloaderProcessStep.Idle;

        /// <summary>
        /// Triggered when state changes
        /// </summary>
        public event EventHandler<BootloaderProcessStep>? StepChanged;

        /// <summary>
        /// Transition to a new step with validation
        /// </summary>
        public void TransitionTo(BootloaderProcessStep newStep)
        {
            if (!IsValidTransition(CurrentStep, newStep))
            {
                throw new InvalidOperationException(
                    $"Invalid state transition: {CurrentStep} → {newStep}");
            }

            var oldStep = CurrentStep;
            CurrentStep = newStep;
            StepChanged?.Invoke(this, newStep);
        }

        /// <summary>
        /// Force set state without validation (use sparingly)
        /// </summary>
        public void ForceSet(BootloaderProcessStep step)
        {
            CurrentStep = step;
            StepChanged?.Invoke(this, step);
        }

        /// <summary>
        /// Reset to idle state
        /// </summary>
        public void Reset()
        {
            CurrentStep = BootloaderProcessStep.Idle;
            StepChanged?.Invoke(this, CurrentStep);
        }

        /// <summary>
        /// Check if state transition is valid
        /// </summary>
        private bool IsValidTransition(BootloaderProcessStep from, BootloaderProcessStep to)
        {
            // Allow transition to Failed or Idle from any state
            if (to == BootloaderProcessStep.Failed || to == BootloaderProcessStep.Idle)
                return true;

            // Allow transition to Complete from End or Transfer
            if (to == BootloaderProcessStep.Complete)
                return from == BootloaderProcessStep.End || from == BootloaderProcessStep.Transfer;

            // Sequential transitions
            return to switch
            {
                BootloaderProcessStep.EnterBootloader => from == BootloaderProcessStep.Idle,
                BootloaderProcessStep.Ping => from == BootloaderProcessStep.Idle || 
                                               from == BootloaderProcessStep.EnterBootloader,
                BootloaderProcessStep.Begin => from == BootloaderProcessStep.Ping,
                BootloaderProcessStep.Transfer => from == BootloaderProcessStep.Begin || 
                                                   from == BootloaderProcessStep.Transfer,
                BootloaderProcessStep.End => from == BootloaderProcessStep.Transfer,
                BootloaderProcessStep.Reset => from == BootloaderProcessStep.End || 
                                                from == BootloaderProcessStep.Complete,
                _ => false
            };
        }

        /// <summary>
        /// Get human-readable description of current step
        /// </summary>
        public string GetStepDescription() => CurrentStep switch
        {
            BootloaderProcessStep.Idle => "Idle - Ready for operations",
            BootloaderProcessStep.EnterBootloader => "Entering bootloader mode",
            BootloaderProcessStep.Ping => "Testing bootloader communication",
            BootloaderProcessStep.Begin => "Preparing for firmware transfer",
            BootloaderProcessStep.Transfer => "Transferring firmware data",
            BootloaderProcessStep.End => "Finalizing firmware update",
            BootloaderProcessStep.Reset => "Resetting device",
            BootloaderProcessStep.Complete => "Update completed successfully",
            BootloaderProcessStep.Failed => "Operation failed",
            _ => "Unknown state"
        };
    }
}
