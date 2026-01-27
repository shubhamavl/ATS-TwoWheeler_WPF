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
            
            _viewModel.OpenSettingsRequested += () => 
            {
                var settingsWindow = new SettingsWindow
                {
                    DataContext = _viewModel,
                    Owner = this
                };
                settingsWindow.Show(); 
            };

            _viewModel.OpenConfigViewerRequested += () =>
            {
                var viewer = new ConfigurationViewer { Owner = this };
                viewer.ShowDialog();
            };

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.Cleanup();
        }
    }
}