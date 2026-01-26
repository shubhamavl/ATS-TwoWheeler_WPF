using System.Windows;
using System.IO;
using ATS_TwoWheeler_WPF.Views;

namespace ATS_TwoWheeler_WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize dependency registry
            ATS_TwoWheeler_WPF.Core.ServiceRegistry.InitializeDefaultServices();

            // Set up global exception handling
            this.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"System Error: {args.Exception.Message}",
                               "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Manually create and show MainWindow
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup code here if needed
            base.OnExit(e);
        }
    }
}