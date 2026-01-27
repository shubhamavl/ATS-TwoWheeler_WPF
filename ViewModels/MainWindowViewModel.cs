using System;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        // Services
        private readonly ICANService _canService;
        private readonly IWeightProcessorService _weightProcessor;
        private readonly IDataLoggerService _dataLogger;
        private readonly ISettingsService _settings;

        // Child ViewModels
        public ConnectionViewModel Connection { get; }
        public DashboardViewModel Dashboard { get; }
        public CalibrationViewModel Calibration { get; }
        public SystemStatusPanelViewModel SystemStatus { get; }
        public LoggingPanelViewModel Logging { get; }
        public AppStatusBarViewModel StatusBar { get; }
        public SettingsViewModel Settings { get; }
        
        // Timer for UI updates (polling high-frequency data)
        private readonly DispatcherTimer _uiTimer;

        // UI State


        // Commands
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenConfigViewerCommand { get; }
        public ICommand StopAllCommand { get; }
        
        // Events
        public event Action? OpenSettingsRequested;
        public event Action? OpenConfigViewerRequested;
        
        public MainWindowViewModel()
        {
            // Resolve Services via registry
            _canService = ServiceRegistry.GetService<ICANService>();
            _weightProcessor = ServiceRegistry.GetService<IWeightProcessorService>();
            _dataLogger = ServiceRegistry.GetService<IDataLoggerService>();
            _settings = ServiceRegistry.GetService<ISettingsService>();
            var navigationService = ServiceRegistry.GetService<INavigationService>();
            
            var updateService = ServiceRegistry.GetService<IUpdateService>();
            var dialogService = ServiceRegistry.GetService<IDialogService>();
            
            _weightProcessor.Start(); // Start processing thread
            
            // Initialize Child ViewModels
            Connection = new ConnectionViewModel(_canService, _settings);
            Dashboard = new DashboardViewModel(_weightProcessor, _canService, _settings);
            Calibration = new CalibrationViewModel(_weightProcessor, _canService, _settings, navigationService);
            SystemStatus = new SystemStatusPanelViewModel(_canService, navigationService);
            Logging = new LoggingPanelViewModel(_dataLogger, _canService);
            StatusBar = new AppStatusBarViewModel(_canService, updateService, dialogService);
            Settings = new SettingsViewModel(_settings);
            
            // Commands
            OpenSettingsCommand = new RelayCommand(_ => OnOpenSettings());
            OpenConfigViewerCommand = new RelayCommand(_ => OpenConfigViewerRequested?.Invoke());
            StopAllCommand = new RelayCommand(OnStopAll);
            
            // Setup UI Timer
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 Hz default
            };
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();
        }

        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            // Refresh ViewModels that rely on polling (like high-freq weight data)
            Dashboard.Refresh();
            Logging.Refresh();
            StatusBar.Refresh();
            SystemStatus.Refresh();
           
            // Polling for connection state if not fully event-driven
            // ConnectionViewModel usually updates via internal logic or manual refresh, 
            // but we can ensure sync here if needed.
        }

        private void OnStopAll(object? parameter)
        {
            _canService.StopAllStreams();
            _dataLogger.StopLogging();
            Connection.IsStreaming = false;
        }

        private void OnOpenSettings()
        {
            OpenSettingsRequested?.Invoke();
        }

        public void Cleanup()
        {
            _uiTimer.Stop();
            _weightProcessor.Stop();
            _canService.Disconnect();
        }
    }
}
