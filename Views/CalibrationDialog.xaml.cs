using System.Windows;
using ATS_TwoWheeler_WPF.ViewModels;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class CalibrationDialog : Window
    {
        public CalibrationDialog(CalibrationDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Wire up ViewModel events
            viewModel.RequestClose += (s, e) => Close();
            viewModel.CalculationCompleted += OnCalculationCompleted;

            // Link close event to Dispose of VM
            Closed += (s, e) => viewModel.Dispose();
        }

        private void OnCalculationCompleted(object? sender, Models.CalibrationDialogResultsEventArgs e)
        {
            if (e.IsSuccessful)
            {
                PopupEquationTxt.Text = $"Internal: {e.InternalEquation}\nADS1115: {e.AdsEquation}";
                ViewResultsBtn.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InfoBtn_Click(object sender, RoutedEventArgs e)
        {
            InstructionsPopup.IsOpen = true;
        }

        private void CloseInstructionsPopup_Click(object sender, RoutedEventArgs e)
        {
            InstructionsPopup.IsOpen = false;
        }

        private void ViewResultsBtn_Click(object sender, RoutedEventArgs e)
        {
            ResultsPopup.IsOpen = true;
        }

        private void CloseResultsPopup_Click(object sender, RoutedEventArgs e)
        {
            ResultsPopup.IsOpen = false;
        }
    }
}
