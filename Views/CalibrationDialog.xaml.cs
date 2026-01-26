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
            
            // Link close event to Dispose of VM
            Closed += (s, e) => viewModel.Dispose();
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
