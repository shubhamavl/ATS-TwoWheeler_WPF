using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using ATS_TwoWheeler_WPF.ViewModels;
using ATS_TwoWheeler_WPF.ViewModels.Bootloader;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class BootloaderManagerWindow : Window
    {
        private readonly BootloaderViewModel _viewModel;

        public BootloaderManagerWindow(BootloaderViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;

            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Initialize step visuals
            UpdateStepVisuals(_viewModel.CurrentStep);

            // Link to ViewModel disposal on window close
            Closed += (s, e) =>
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Dispose();
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BootloaderViewModel.CurrentStep))
            {
                UpdateStepVisuals(_viewModel.CurrentStep);
            }
        }

        private void UpdateStepVisuals(BootloaderProcessStep currentStep)
        {
            // Reset all steps to pending
            ResetStep(Step1Border, Step1Status);
            ResetStep(Step2Border, Step2Status);
            ResetStep(Step3Border, Step3Status);
            ResetStep(Step4Border, Step4Status);
            ResetStep(Step5Border, Step5Status);
            ResetStep(Step6Border, Step6Status);

            // Update based on current step
            switch (currentStep)
            {
                case BootloaderProcessStep.Idle:
                    // All pending
                    break;

                case BootloaderProcessStep.EnterBootloader:
                    SetStepInProgress(Step1Border, Step1Status, "Entering...");
                    break;

                case BootloaderProcessStep.Ping:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepInProgress(Step2Border, Step2Status, "Pinging...");
                    break;

                case BootloaderProcessStep.Begin:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepComplete(Step2Border, Step2Status);
                    SetStepInProgress(Step3Border, Step3Status, "Beginning...");
                    break;

                case BootloaderProcessStep.Transfer:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepComplete(Step2Border, Step2Status);
                    SetStepComplete(Step3Border, Step3Status);
                    SetStepInProgress(Step4Border, Step4Status, "Transferring...");
                    break;

                case BootloaderProcessStep.End:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepComplete(Step2Border, Step2Status);
                    SetStepComplete(Step3Border, Step3Status);
                    SetStepComplete(Step4Border, Step4Status);
                    SetStepInProgress(Step5Border, Step5Status, "Finalizing...");
                    break;

                case BootloaderProcessStep.Reset:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepComplete(Step2Border, Step2Status);
                    SetStepComplete(Step3Border, Step3Status);
                    SetStepComplete(Step4Border, Step4Status);
                    SetStepComplete(Step5Border, Step5Status);
                    SetStepInProgress(Step6Border, Step6Status, "Resetting...");
                    break;

                case BootloaderProcessStep.Complete:
                    SetStepComplete(Step1Border, Step1Status);
                    SetStepComplete(Step2Border, Step2Status);
                    SetStepComplete(Step3Border, Step3Status);
                    SetStepComplete(Step4Border, Step4Status);
                    SetStepComplete(Step5Border, Step5Status);
                    SetStepComplete(Step6Border, Step6Status);
                    break;

                case BootloaderProcessStep.Failed:
                    // Mark current step as failed (simplified - marks last attempted)
                    SetStepFailed(Step1Border, Step1Status);
                    break;
            }
        }

        private void ResetStep(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock status)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); // Light gray
            status.Text = "Pending";
            status.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray
        }

        private void SetStepInProgress(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock status, string text = "In Progress")
        {
            border.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber/Yellow
            status.Text = text;
            status.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Black
        }

        private void SetStepComplete(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock status)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green
            status.Text = "Complete";
            status.Foreground = new SolidColorBrush(Colors.White);
        }

        private void SetStepFailed(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock status)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
            status.Text = "Failed";
            status.Foreground = new SolidColorBrush(Colors.White);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
