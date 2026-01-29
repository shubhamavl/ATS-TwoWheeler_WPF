using System.Windows;
using System.IO;
using ATS_TwoWheeler_WPF.Views;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ATS_TwoWheeler_WPF
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Initialize legacy ServiceRegistry bridge
            ATS_TwoWheeler_WPF.Core.ServiceRegistry.SetProvider(ServiceProvider);

            // Enhanced global exception handling
            this.DispatcherUnhandledException += (sender, args) =>
            {
                var logger = ServiceProvider.GetService<IProductionLoggerService>() ?? ProductionLogger.Instance;
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
                var logger = ServiceProvider.GetService<IProductionLoggerService>() ?? ProductionLogger.Instance;
                var ex = args.ExceptionObject as System.Exception;
                logger.LogError($"Unhandled Thread Exception: {ex?.Message}", "Global");
                if (ex != null)
                {
                    logger.LogError($"Stack Trace: {ex.StackTrace}", "Global");
                }
            };

            // Resolve and show MainWindow
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<ISettingsService>(SettingsManager.Instance);
            services.AddSingleton<ICANService, CANService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IDataLoggerService, DataLogger>();
            services.AddSingleton<IProductionLoggerService>(ProductionLogger.Instance);
            services.AddSingleton<StatusHistoryManager>();
            
            // Weight & Tare
            services.AddSingleton<TareManager>(provider => {
                var tm = new TareManager();
                tm.LoadFromFile();
                return tm;
            });
            services.AddSingleton<IWeightProcessorService, WeightProcessor>(provider => {
                var wp = new WeightProcessor();
                wp.SetTareManager(provider.GetRequiredService<TareManager>());
                return wp;
            });

            // Bootloader Services
            var canService = services.BuildServiceProvider().GetRequiredService<ICANService>(); // Circular dep workaround for simple DI plan, better to resolve in factory
            
            services.AddSingleton<BootloaderDiagnosticsService>();
            services.AddSingleton<IBootloaderDiagnosticsService>(provider => provider.GetRequiredService<BootloaderDiagnosticsService>());
            
            // FirmwareUpdateService requires CANService
            services.AddSingleton<FirmwareUpdateService>();
            services.AddSingleton<IFirmwareUpdateService>(provider => provider.GetRequiredService<FirmwareUpdateService>());

            // Status Monitor
            services.AddSingleton<IStatusMonitorService>(provider => {
                var sm = new StatusMonitorService(
                    provider.GetRequiredService<ICANService>(),
                    provider.GetRequiredService<IDialogService>()
                );
                sm.StartMonitoring();
                return sm;
            });

            // ViewModels
            services.AddTransient<ATS_TwoWheeler_WPF.ViewModels.ConnectionViewModel>();
            services.AddTransient<ATS_TwoWheeler_WPF.ViewModels.TwoWheelerWeightViewModel>();
            services.AddTransient<ATS_TwoWheeler_WPF.ViewModels.SettingsViewModel>();
            services.AddTransient<ATS_TwoWheeler_WPF.ViewModels.BootloaderViewModel>();
            services.AddTransient<ATS_TwoWheeler_WPF.ViewModels.LogsViewModel>();
            services.AddSingleton<ATS_TwoWheeler_WPF.ViewModels.MainWindowViewModel>();

            // Views
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup code here if needed
            base.OnExit(e);
        }
    }
}
