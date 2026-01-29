using System;
using System.Windows.Input;
using System.Windows;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;

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

        private bool _isBrakeMode;
        public bool IsBrakeMode
        {
            get => _isBrakeMode;
            set => SetProperty(ref _isBrakeMode, value);
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
            _weightProcessor.Tare();
        }

        private void OnCalibrate(object? parameter)
        {
            _navigationService.ShowCalibrationDialog(IsBrakeMode);
        }

        private void OnResetCalibration(object? parameter)
        {
            if (MessageBox.Show("Are you sure you want to reset calibration? This will delete the current calibration file.",
                "Reset Calibration", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                LinearCalibration.DeleteCalibration(_canService.CurrentADCMode, SystemModeText == "Brake" ? SystemMode.Brake : SystemMode.Weight);
                _weightProcessor.LoadCalibration();
            }
        }

        private void OnResetTare(object? parameter)
        {
            _weightProcessor.ResetTare();
        }

        private void OnSwitchSystemMode(object? parameter)
        {
            // Toggle system mode (Weight vs Brake)
            // SystemStatusPanelViewModel usually updates UI based on CAN response
            bool targetBrakeMode = SystemModeText == "Weight"; // If current is Weight, switch to Brake
            _canService.SwitchSystemMode(targetBrakeMode ? SystemMode.Brake : SystemMode.Weight);
        }

        private void OnSwitchAdcMode(object? parameter)
        {
            if (_canService.CurrentADCMode == AdcMode.InternalWeight)
            {
                _canService.SwitchToADS1115();
            }
            else
            {
                _canService.SwitchToInternalADC();
            }
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

        public void UpdateSystemStatus(AdcMode adcMode, SystemMode systemMode)
        {
            AdcModeText = adcMode == AdcMode.Ads1115 ? "ADS1115" : "Internal";
            IsBrakeMode = systemMode == SystemMode.Brake;
            SystemModeText = IsBrakeMode ? "Brake" : "Weight";
        }
    }
}
