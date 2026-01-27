using System;
using System.Windows.Input;
using System.Windows.Media;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Models;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class SystemStatusPanelViewModel : BaseViewModel
    {
        private readonly ICANService? _canService;
        
        // Properties
        private string _statusText = "Unknown";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private Brush _statusColor = Brushes.Gray;
        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        private string _relayStateText = "--";
        public string RelayStateText
        {
            get => _relayStateText;
            set => SetProperty(ref _relayStateText, value);
        }
        
        private Brush _relayStateColor = Brushes.Black;
        public Brush RelayStateColor
        {
            get => _relayStateColor;
            set => SetProperty(ref _relayStateColor, value);
        }

        private string _canRateText = "-- Hz";
        public string CanRateText
        {
            get => _canRateText;
            set => SetProperty(ref _canRateText, value);
        }

        private string _adcRateText = "-- Hz";
        public string AdcRateText
        {
            get => _adcRateText;
            set => SetProperty(ref _adcRateText, value);
        }

        private string _uptimeText = "00:00:00";
        public string UptimeText
        {
            get => _uptimeText;
            set => SetProperty(ref _uptimeText, value);
        }

        private string _firmwareVersionText = "--";
        public string FirmwareVersionText
        {
            get => _firmwareVersionText;
            set => SetProperty(ref _firmwareVersionText, value);
        }

        private string _lastUpdateText = "--";
        public string LastUpdateText
        {
            get => _lastUpdateText;
            set => SetProperty(ref _lastUpdateText, value);
        }



        // Commands
        public ICommand RequestStatusCommand { get; }
        public ICommand ShowHistoryCommand { get; }

        private readonly INavigationService? _navigationService;

        public SystemStatusPanelViewModel(ICANService? canService, INavigationService? navigationService)
        {
            _canService = canService;
            _navigationService = navigationService;
            
            RequestStatusCommand = new RelayCommand(OnRequestStatus);
            ShowHistoryCommand = new RelayCommand(OnShowHistory);
            
            // Subscribe to events
            if (_canService != null)
            {
                _canService.SystemStatusReceived += OnSystemStatusReceived;
                _canService.PerformanceMetricsReceived += OnPerformanceMetricsReceived;
                _canService.FirmwareVersionReceived += OnFirmwareVersionReceived;
            }
        }



        private void OnSystemStatusReceived(object? sender, SystemStatusEventArgs e)
        {
            // Helper to run on UI thread if needed (in pure MVVM, platform scheduler handles this, 
            // but for simplicity here we rely on binding or Dispatcher if needed. 
            // Since this is called from background thread usually, we might need dispatching.
            // But we'll implement properties to just update. The View binding engine usually handles cross-thread prop changes in newer WPF, 
            // or we use Application.Current.Dispatcher)
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Status Color & Text
                StatusColor = e.SystemStatus switch
                {
                    SystemStatus.Ok => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // Green - OK
                    SystemStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),   // Yellow - Warning
                    SystemStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),   // Red - Error
                    _ => new SolidColorBrush(Color.FromRgb(128, 128, 128)) // Gray - Unknown
                };
                
                string status = e.SystemStatus switch
                {
                    SystemStatus.Ok => "OK",
                    SystemStatus.Warning => "Warn",
                    SystemStatus.Error => "ERROR",
                    _ => "???"
                };
                if (e.ErrorFlags != 0) status += $" (0x{e.ErrorFlags:X2})";
                StatusText = status;
                
                // Relay State
                RelayStateText = e.RelayState == SystemMode.Weight ? "Weight" : "Brake";
                RelayStateColor = e.RelayState == SystemMode.Weight ? Brushes.Blue : Brushes.Red;
                
                // Uptime
                TimeSpan t = TimeSpan.FromSeconds(e.UptimeSeconds);
                UptimeText = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
                
                LastUpdateText = e.Timestamp.ToString("HH:mm:ss");
            });
        }

        private void OnPerformanceMetricsReceived(object? sender, PerformanceMetricsEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CanRateText = $"{e.CanTxHz} Hz";
                AdcRateText = $"{e.AdcSampleHz} Hz";
            });
        }

        private void OnFirmwareVersionReceived(object? sender, FirmwareVersionEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                FirmwareVersionText = $"FW: {e.VersionString}";
            });
        }

        private void OnRequestStatus(object? parameter)
        {
            _canService?.RequestSystemStatus();
        }

        public void Refresh()
        {
            // Update counts if needed here
        }

        private void OnShowHistory(object? parameter)
        {
            _navigationService?.ShowLogsWindow();
        }
    }
}
