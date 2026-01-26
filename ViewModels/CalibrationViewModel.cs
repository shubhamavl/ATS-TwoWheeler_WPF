using System;
using System.Windows.Input;
using System.Windows;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class CalibrationViewModel : BaseViewModel
    {
        private readonly IWeightProcessorService _weightProcessor;
        private readonly ICANService _canService;
        private readonly ISettingsService _settings;
        private readonly INavigationService _navigationService;

        private string _calStatusText = "Uncalibrated";
        public string CalStatusText
        {
            get => _calStatusText;
            set => SetProperty(ref _calStatusText, value);
        }
        
        private string _tareStatusText = "Tare: --";
        public string TareStatusText
        {
            get => _tareStatusText;
            set => SetProperty(ref _tareStatusText, value);
        }

        private string _systemModeText = "Weight";
        public string SystemModeText
        {
            get => _systemModeText;
            set => SetProperty(ref _systemModeText, value);
        }
        
        private string _adcModeText = "Internal";
        public string AdcModeText
        {
            get => _adcModeText;
            set => SetProperty(ref _adcModeText, value);
        }

        public ICommand TareCommand { get; }
        public ICommand CalibrateCommand { get; }
        public ICommand ResetCalibrationCommand { get; }
        public ICommand ResetTareCommand { get; }
        public ICommand SwitchSystemModeCommand { get; }
        public ICommand SwitchAdcModeCommand { get; }
        public ICommand OpenTwoWheelerCommand { get; }
        public ICommand OpenBootloaderCommand { get; }
        public ICommand OpenMonitorCommand { get; }
        public ICommand OpenLogsCommand { get; }

        public CalibrationViewModel(IWeightProcessorService weightProcessor, ICANService canService, ISettingsService settings, INavigationService navigationService)
        {
            _weightProcessor = weightProcessor;
            _canService = canService;
            _settings = settings;
            _navigationService = navigationService;

            TareCommand = new RelayCommand(OnTare);
            CalibrateCommand = new RelayCommand(OnCalibrate);
            ResetCalibrationCommand = new RelayCommand(OnResetCalibration);
            ResetTareCommand = new RelayCommand(OnResetTare);
            SwitchSystemModeCommand = new RelayCommand(OnSwitchSystemMode);
            SwitchAdcModeCommand = new RelayCommand(OnSwitchAdcMode);
            OpenTwoWheelerCommand = new RelayCommand(OnOpenTwoWheeler);
            OpenBootloaderCommand = new RelayCommand(OnOpenBootloader);
            OpenMonitorCommand = new RelayCommand(OnOpenMonitor);
            OpenLogsCommand = new RelayCommand(OnOpenLogs);
        }

        private void OnTare(object? parameter)
        {
             // ...
        }

        private void OnCalibrate(object? parameter)
        {
             _navigationService.ShowCalibrationDialog();
        }

        private void OnResetCalibration(object? parameter)
        {
            // ...
        }

        private void OnResetTare(object? parameter)
        {
             // ...
        }

        private void OnSwitchSystemMode(object? parameter)
        {
             // ...
        }

        private void OnSwitchAdcMode(object? parameter)
        {
            // ...
        }

        private void OnOpenTwoWheeler(object? parameter)
        {
            _navigationService.ShowTwoWheelerWindow();
        }

        private void OnOpenBootloader(object? parameter)
        {
            _navigationService.ShowBootloaderManager();
        }

        private void OnOpenMonitor(object? parameter)
        {
            _navigationService.ShowMonitorWindow();
        }

        private void OnOpenLogs(object? parameter)
        {
            _navigationService.ShowLogsWindow();
        }
    }
}
