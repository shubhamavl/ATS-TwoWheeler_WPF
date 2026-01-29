using System;
using System.Windows;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.ViewModels;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class LogsWindow : Window
    {
        public LogsWindow(ProductionLogger? logger, DataLogger? dataLogger = null)
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup ViewModel resources if possible
            if (DataContext is LogsViewModel vm)
            {
                vm.Cleanup();
            }
            base.OnClosed(e);
        }
    }
}
