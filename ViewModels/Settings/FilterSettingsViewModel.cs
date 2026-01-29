using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for weight filtering settings
    /// </summary>
    public class FilterSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;

        public FilterSettingsViewModel(ISettingsService settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        public string Type
        {
            get => _settingsManager.Settings.FilterType.ToString();
            set
            {
                if (Enum.TryParse(value, out FilterType newType) && _settingsManager.Settings.FilterType != newType)
                {
                    _settingsManager.Settings.FilterType = newType;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public double Alpha
        {
            get => _settingsManager.Settings.FilterAlpha;
            set
            {
                if (Math.Abs(_settingsManager.Settings.FilterAlpha - value) > 0.001)
                {
                    _settingsManager.Settings.FilterAlpha = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int WindowSize
        {
            get => _settingsManager.Settings.FilterWindowSize;
            set
            {
                if (_settingsManager.Settings.FilterWindowSize != value)
                {
                    _settingsManager.Settings.FilterWindowSize = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool Enabled
        {
            get => _settingsManager.Settings.FilterEnabled;
            set
            {
                if (_settingsManager.Settings.FilterEnabled != value)
                {
                    _settingsManager.Settings.FilterEnabled = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }
    }
}
