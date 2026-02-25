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

        public MainWindowViewModel(
            ICANService canService,
            IWeightProcessorService weightProcessor,
            IDataLoggerService dataLogger,
            ISettingsService settings,
            INavigationService navigationService,
            IUpdateService updateService,
            IDialogService dialogService,
            IStatusMonitorService statusMonitor,
            StatusHistoryManager historyManager)
        {
            // Resolve Services
            _canService = canService;
            _weightProcessor = weightProcessor;
            _dataLogger = dataLogger;
            _settings = settings;

            _weightProcessor.Start(); // Start processing thread

            // Initialize Child ViewModels
            Connection = new ConnectionViewModel(_canService, _settings);
            Dashboard = new DashboardViewModel(_weightProcessor, _canService, _settings);
            Calibration = new CalibrationViewModel(_weightProcessor, _canService, _settings, navigationService);
            SystemStatus = new SystemStatusPanelViewModel(_canService, navigationService, statusMonitor, historyManager);
            Logging = new LoggingPanelViewModel(_dataLogger, _canService);
            StatusBar = new AppStatusBarViewModel(_canService, updateService, dialogService);
            Settings = new SettingsViewModel(_settings);

            // Wire up CAN Service to Weight Processor and UI
            _canService.RawDataReceived += OnRawDataReceived;
            _canService.SystemStatusReceived += OnSystemStatusReceived;

            // Commands
            OpenSettingsCommand = new RelayCommand(_ => OnOpenSettings());
            OpenConfigViewerCommand = new RelayCommand(_ => OpenConfigViewerRequested?.Invoke());
            StopAllCommand = new RelayCommand(OnStopAll);

            // Setup UI Timer
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settings.Settings.UIUpdateRateMs)
            };
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();

            // Subscribe to settings changes
            _settings.SettingsChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            _uiTimer.Interval = TimeSpan.FromMilliseconds(_settings.Settings.UIUpdateRateMs);
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

        private void OnRawDataReceived(object? sender, RawDataEventArgs e)
        {
            _weightProcessor.EnqueueRawData(e.RawADCSum);
        }

        private void OnSystemStatusReceived(object? sender, SystemStatusEventArgs e)
        {
            // Sync Dashboard state
            Dashboard.UpdateSystemStatus(e.ADCMode, e.RelayState);

            // Sync Calibration state
            Calibration.UpdateSystemStatus(e.ADCMode, e.RelayState);

            // Sync WeightProcessor mode
            _weightProcessor.SetADCMode(e.ADCMode);
            _weightProcessor.SetBrakeMode(e.RelayState != 0);

            // Reset filters on mode change to prevent carry-over from different hardware states
            _weightProcessor.ResetFilters();
        }

        public void Cleanup()
        {
            _canService.RawDataReceived -= OnRawDataReceived;
            _canService.SystemStatusReceived -= OnSystemStatusReceived;
            _uiTimer.Stop();
            _weightProcessor.Stop();
            _canService.Disconnect();
        }
    }
}
