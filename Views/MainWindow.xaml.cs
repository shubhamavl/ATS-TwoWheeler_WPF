using System;
using System.Windows;
using ATS_TwoWheeler_WPF.ViewModels;

namespace ATS_TwoWheeler_WPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize ViewModel
            _viewModel = new MainWindowViewModel();
            this.DataContext = _viewModel;
            
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Cleanup();
        }
    }
}