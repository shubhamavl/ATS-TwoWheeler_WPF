using System.Windows;
using ATS_TwoWheeler_WPF.ViewModels;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class ConfigurationViewer : Window
    {
        public ConfigurationViewer()
        {
            InitializeComponent();

            // Resolve ViewModel
            var settings = ServiceRegistry.GetService<ISettingsService>();
            var dialog = ServiceRegistry.GetService<IDialogService>();
            DataContext = new ConfigurationViewerViewModel(settings, dialog);
        }
    }
}
