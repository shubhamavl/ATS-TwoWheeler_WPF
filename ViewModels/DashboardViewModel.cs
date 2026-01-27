using System;
using System.Windows.Media;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly IWeightProcessorService _weightProcessor;
        private readonly ICANService _canService;
        private readonly ISettingsService _settings;

        // Big Weight Display
        private string _weightText = "0.0 kg";
        public string WeightText
        {
            get => _weightText;
            set => SetProperty(ref _weightText, value);
        }

        private Brush _weightColor = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green
        public Brush WeightColor
        {
            get => _weightColor;
            set => SetProperty(ref _weightColor, value);
        }

        // Indicators
        private string _streamStatusText = "Stopped";
        public string StreamStatusText
        {
            get => _streamStatusText;
            set => SetProperty(ref _streamStatusText, value);
        }

        private Brush _streamIndicatorColor = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
        public Brush StreamIndicatorColor
        {
            get => _streamIndicatorColor;
            set => SetProperty(ref _streamIndicatorColor, value);
        }

        private string _systemModeText = "Weight";
        public string SystemModeText
        {
            get => _systemModeText;
            set => SetProperty(ref _systemModeText, value);
        }

        private string _adcModeText = "12-bit";
        public string AdcModeText
        {
            get => _adcModeText;
            set => SetProperty(ref _adcModeText, value);
        }

        private string _rawAdcText = "0";
        public string RawAdcText
        {
            get => _rawAdcText;
            set => SetProperty(ref _rawAdcText, value);
        }

        private string _calStatusText = "Calibrated";
        public string CalStatusText
        {
            get => _calStatusText;
            set => SetProperty(ref _calStatusText, value);
        }

        private string _tareStatusText = "Tare: 0.0";
        public string TareStatusText
        {
            get => _tareStatusText;
            set => SetProperty(ref _tareStatusText, value);
        }

        private bool _isBrakeMode;
        public bool IsBrakeMode
        {
            get => _isBrakeMode;
            set => SetProperty(ref _isBrakeMode, value);
        }
        public DashboardViewModel(IWeightProcessorService weightProcessor, ICANService canService, ISettingsService settings)
        {
            _weightProcessor = weightProcessor;
            _canService = canService;
            _settings = settings;
            
            // Subscribe to updates
            // Need a timer or event from WeightProcessor for weight updates.
            // WeightProcessor runs on background thread, but doesn't seem to fire "WeightUpdated" event in interface.
            // I should add one or use a timer in MainWindowViewModel to refresh this VM.
            // For now, I'll add a Refresh() method.
        }

        public void Refresh()
        {
            var data = _weightProcessor.LatestTotal;
            if (data != null)
            {
                RawAdcText = data.RawADC.ToString();
                double weight = data.TaredWeight;
                
                if (IsBrakeMode)
                {
                    weight *= 9.80665;
                    WeightText = $"{weight:F1} N";
                }
                else
                {
                    WeightText = $"{weight:F1} kg";
                }
                
                TareStatusText = $"Tare: {data.TareValue:F1} kg";
            }

            // Sync with CAN state
            StreamStatusText = _canService.IsConnected ? "Connected" : "Disconnected";
            StreamIndicatorColor = _canService.IsConnected ? new SolidColorBrush(Color.FromRgb(39, 174, 96)) : new SolidColorBrush(Color.FromRgb(220, 53, 69));
            
            CalStatusText = (_weightProcessor.InternalCalibration?.IsValid == true) ? "Calibrated (Internal)" : "Uncalibrated";
            if (_weightProcessor.Ads1115Calibration?.IsValid == true) CalStatusText = "Calibrated (ADS)";
        }
        
        public void UpdateSystemStatus(byte adcMode, byte relayState)
        {
             AdcModeText = adcMode == 1 ? "ADS1115 16-bit" : "Internal 12-bit";
             SystemModeText = relayState == 0 ? "Weight" : "Brake";
             IsBrakeMode = relayState != 0;
        }
    }
}
