using System;
using System.Windows.Media;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;

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

        private double _peakWeight;
        public double PeakWeight
        {
            get => _peakWeight;
            private set => SetProperty(ref _peakWeight, value);
        }

        private string _peakWeightText = "Peak: 0.0 kg";
        public string PeakWeightText
        {
            get => _peakWeightText;
            set => SetProperty(ref _peakWeightText, value);
        }

        public ICommand ResetPeakCommand { get; }
        public DashboardViewModel(IWeightProcessorService weightProcessor, ICANService canService, ISettingsService settings)
        {
            _weightProcessor = weightProcessor;
            _canService = canService;
            _settings = settings;

            ResetPeakCommand = new RelayCommand(_ => ResetPeak());
        }

        private void ResetPeak()
        {
            PeakWeight = 0;
            UpdatePeakText();
        }

        public void Refresh()
        {
            var data = _weightProcessor.LatestTotal;
            if (data != null)
            {
                RawAdcText = data.RawADC.ToString();
                double weight = data.TaredWeight;

                // Calibration Check
                var internalCal = _weightProcessor.InternalCalibration;
                var adsCal = _weightProcessor.Ads1115Calibration;
                bool isCalibrated = (internalCal?.IsValid == true) || (adsCal?.IsValid == true);

                if (!isCalibrated)
                {
                    WeightText = "Calibrate first";
                    WeightColor = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red for warning
                }
                else
                {
                    WeightColor = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Restore Green
                    if (IsBrakeMode)
                    {
                        // Track peak
                        if (Math.Abs(weight) > PeakWeight)
                        {
                            PeakWeight = Math.Abs(weight);
                        }

                        if (_settings.Settings.BrakeDisplayUnit == "N")
                        {
                            weight *= _settings.Settings.BrakeKgToNewtonMultiplier;
                            WeightText = $"{weight:F1} N";
                        }
                        else
                        {
                            WeightText = $"{weight:F1} kg";
                        }
                    }
                    else
                    {
                        WeightText = $"{weight:F1} kg";
                    }
                }

                UpdatePeakText();
                TareStatusText = $"Tare: {data.TareValue:F1} kg";
            }

            // Sync with CAN state
            if (_canService.IsStreaming)
            {
                StreamStatusText = "Streaming";
                StreamIndicatorColor = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green
            }
            else
            {
                StreamStatusText = "Stopped";
                StreamIndicatorColor = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber/Yellow for stopped but connected? Or just Red/Grey?
                // Using Red for Stopped to match original style, or Orange for 'Ready but stopped' if connected.
                // Let's stick to Red for Stopped for now for clarity.
                StreamIndicatorColor = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            }

            CalStatusText = (_weightProcessor.InternalCalibration?.IsValid == true) ? "Calibrated (Internal)" : "Uncalibrated";
            if (_weightProcessor.Ads1115Calibration?.IsValid == true)
            {
                CalStatusText = "Calibrated (ADS)";
            }
        }

        private void UpdatePeakText()
        {
            if (IsBrakeMode && _settings.Settings.BrakeDisplayUnit == "N")
            {
                double peakN = PeakWeight * _settings.Settings.BrakeKgToNewtonMultiplier;
                PeakWeightText = $"Peak: {peakN:F1} N";
            }
            else
            {
                PeakWeightText = $"Peak: {PeakWeight:F1} kg";
            }
        }

        public void UpdateSystemStatus(AdcMode adcMode, SystemMode relayState)
        {
            AdcModeText = adcMode == AdcMode.Ads1115 ? "ADS1115 16-bit" : "Internal 12-bit";
            IsBrakeMode = relayState == SystemMode.Brake;
            SystemModeText = IsBrakeMode ? "Brake" : "Weight";

            // Auto-reset peak when switching to Brake mode if that's desired, 
            // but user usually wants to clear it manually.
            UpdatePeakText();
        }
    }
}
