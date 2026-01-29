using System;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for display and performance settings
    /// </summary>
    public class DisplaySettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;

        public DisplaySettingsViewModel(ISettingsService settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        public int WeightDecimals
        {
            get => _settingsManager.Settings.WeightDisplayDecimals;
            set
            {
                if (_settingsManager.Settings.WeightDisplayDecimals != value)
                {
                    _settingsManager.Settings.WeightDisplayDecimals = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int UIUpdateRateMs
        {
            get => _settingsManager.Settings.UIUpdateRateMs;
            set
            {
                if (_settingsManager.Settings.UIUpdateRateMs != value)
                {
                    _settingsManager.Settings.UIUpdateRateMs = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int DataTimeoutSeconds
        {
            get => _settingsManager.Settings.DataTimeoutSeconds;
            set
            {
                if (_settingsManager.Settings.DataTimeoutSeconds != value)
                {
                    _settingsManager.Settings.DataTimeoutSeconds = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool ShowRawADC
        {
            get => _settingsManager.Settings.ShowRawADC;
            set
            {
                if (_settingsManager.Settings.ShowRawADC != value)
                {
                    _settingsManager.Settings.ShowRawADC = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool ShowStreamingIndicators
        {
            get => _settingsManager.Settings.ShowStreamingIndicators;
            set
            {
                if (_settingsManager.Settings.ShowStreamingIndicators != value)
                {
                    _settingsManager.Settings.ShowStreamingIndicators = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool ShowCalibrationIcons
        {
            get => _settingsManager.Settings.ShowCalibrationIcons;
            set
            {
                if (_settingsManager.Settings.ShowCalibrationIcons != value)
                {
                    _settingsManager.Settings.ShowCalibrationIcons = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }
    }
}
