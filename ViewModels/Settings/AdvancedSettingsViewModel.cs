using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for advanced system settings
    /// </summary>
    public class AdvancedSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;

        public AdvancedSettingsViewModel(ISettingsService settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        public string LogFileFormat
        {
            get => _settingsManager.Settings.LogFileFormat.ToString();
            set
            {
                if (Enum.TryParse(value, out LogFormat format) && _settingsManager.Settings.LogFileFormat != format)
                {
                    _settingsManager.Settings.LogFileFormat = format;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int BatchProcessingSize
        {
            get => _settingsManager.Settings.BatchProcessingSize;
            set
            {
                if (_settingsManager.Settings.BatchProcessingSize != value)
                {
                    _settingsManager.Settings.BatchProcessingSize = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int ClockUpdateIntervalMs
        {
            get => _settingsManager.Settings.ClockUpdateIntervalMs;
            set
            {
                if (_settingsManager.Settings.ClockUpdateIntervalMs != value)
                {
                    _settingsManager.Settings.ClockUpdateIntervalMs = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public string BrakeDisplayUnit
        {
            get => _settingsManager.Settings.BrakeDisplayUnit;
            set
            {
                if (_settingsManager.Settings.BrakeDisplayUnit != value)
                {
                    _settingsManager.Settings.BrakeDisplayUnit = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public double BrakeKgToNewtonMultiplier
        {
            get => _settingsManager.Settings.BrakeKgToNewtonMultiplier;
            set
            {
                if (Math.Abs(_settingsManager.Settings.BrakeKgToNewtonMultiplier - value) > 0.00001)
                {
                    _settingsManager.Settings.BrakeKgToNewtonMultiplier = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }
    }
}
