using System;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for calibration settings and calibration data display
    /// </summary>
    public class CalibrationSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;
        private LinearCalibration? _internalCal;
        private LinearCalibration? _adsCal;

        public CalibrationSettingsViewModel(ISettingsService settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            RefreshCalibrationData();
        }

        #region Calibration Mode

        public string Mode
        {
            get => _settingsManager.Settings.CalibrationMode.ToString();
            set
            {
                if (Enum.TryParse(value, out CalibrationMode mode) && _settingsManager.Settings.CalibrationMode != mode)
                {
                    _settingsManager.Settings.CalibrationMode = mode;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        #endregion

        #region Advanced Calibration Settings

        public bool AveragingEnabled
        {
            get => _settingsManager.Settings.CalibrationAveragingEnabled;
            set
            {
                if (_settingsManager.Settings.CalibrationAveragingEnabled != value)
                {
                    _settingsManager.Settings.CalibrationAveragingEnabled = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int SampleCount
        {
            get => _settingsManager.Settings.CalibrationSampleCount;
            set
            {
                if (_settingsManager.Settings.CalibrationSampleCount != value)
                {
                    _settingsManager.Settings.CalibrationSampleCount = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public int CaptureDurationMs
        {
            get => _settingsManager.Settings.CalibrationCaptureDurationMs;
            set
            {
                if (_settingsManager.Settings.CalibrationCaptureDurationMs != value)
                {
                    _settingsManager.Settings.CalibrationCaptureDurationMs = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool UseMedian
        {
            get => _settingsManager.Settings.CalibrationUseMedian;
            set
            {
                if (_settingsManager.Settings.CalibrationUseMedian != value)
                {
                    _settingsManager.Settings.CalibrationUseMedian = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool RemoveOutliers
        {
            get => _settingsManager.Settings.CalibrationRemoveOutliers;
            set
            {
                if (_settingsManager.Settings.CalibrationRemoveOutliers != value)
                {
                    _settingsManager.Settings.CalibrationRemoveOutliers = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public double OutlierThreshold
        {
            get => _settingsManager.Settings.CalibrationOutlierThreshold;
            set
            {
                if (Math.Abs(_settingsManager.Settings.CalibrationOutlierThreshold - value) > 0.001)
                {
                    _settingsManager.Settings.CalibrationOutlierThreshold = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public double MaxStdDev
        {
            get => _settingsManager.Settings.CalibrationMaxStdDev;
            set
            {
                if (Math.Abs(_settingsManager.Settings.CalibrationMaxStdDev - value) > 0.001)
                {
                    _settingsManager.Settings.CalibrationMaxStdDev = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        public bool ShowQualityMetrics
        {
            get => _settingsManager.Settings.ShowCalibrationQualityMetrics;
            set
            {
                if (_settingsManager.Settings.ShowCalibrationQualityMetrics != value)
                {
                    _settingsManager.Settings.ShowCalibrationQualityMetrics = value;
                    OnPropertyChanged();
                    _settingsManager.SaveSettings();
                }
            }
        }

        #endregion

        #region Calibration Data Display

        public string InternalStatus => _internalCal?.IsValid == true ? "✓ Valid" : "⚠ Not Calibrated";
        public string InternalSlope => _internalCal?.Slope.ToString("F6") ?? "N/A";
        public string InternalIntercept => _internalCal?.Intercept.ToString("F6") ?? "N/A";
        public string InternalDate => _internalCal?.CalibrationDate.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        public string AdsStatus => _adsCal?.IsValid == true ? "✓ Valid" : "⚠ Not Calibrated";
        public string AdsSlope => _adsCal?.Slope.ToString("F6") ?? "N/A";
        public string AdsIntercept => _adsCal?.Intercept.ToString("F6") ?? "N/A";
        public string AdsDate => _adsCal?.CalibrationDate.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        #endregion

        public void RefreshCalibrationData()
        {
            _internalCal = LinearCalibration.LoadFromFile(AdcMode.InternalWeight, SystemMode.Weight);
            _adsCal = LinearCalibration.LoadFromFile(AdcMode.Ads1115, SystemMode.Weight);
            
            OnPropertyChanged(nameof(InternalStatus));
            OnPropertyChanged(nameof(InternalSlope));
            OnPropertyChanged(nameof(InternalIntercept));
            OnPropertyChanged(nameof(InternalDate));
            OnPropertyChanged(nameof(AdsStatus));
            OnPropertyChanged(nameof(AdsSlope));
            OnPropertyChanged(nameof(AdsIntercept));
            OnPropertyChanged(nameof(AdsDate));
        }
    }
}
