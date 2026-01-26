using System.Windows;
using ATS_TwoWheeler_WPF.ViewModels;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class BootloaderManagerWindow : Window
    {
        public BootloaderManagerWindow(BootloaderViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Link to ViewModel disposal on window close
            Closed += (s, e) => viewModel.Dispose();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
