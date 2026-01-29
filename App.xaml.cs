using System.Windows;
using System.IO;
using ATS_TwoWheeler_WPF.Views;
using ATS_TwoWheeler_WPF.Services;

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
            // Enhanced global exception handling
            this.DispatcherUnhandledException += (sender, args) =>
            {
                var logger = ProductionLogger.Instance;
                logger.LogError($"Unhandled Exception: {args.Exception.GetType().Name} - {args.Exception.Message}", "Global");
                logger.LogError($"Stack Trace: {args.Exception.StackTrace}", "Global");
                
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nCheck logs for details.",
                    "System Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                args.Handled = true;
            };

            // Handle background thread exceptions
            System.AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var logger = ProductionLogger.Instance;
                var ex = args.ExceptionObject as System.Exception;
                logger.LogError($"Unhandled Thread Exception: {ex?.Message}", "Global");
                if (ex != null)
                {
                    logger.LogError($"Stack Trace: {ex.StackTrace}", "Global");
                }
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