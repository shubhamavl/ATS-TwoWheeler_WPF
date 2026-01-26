using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsManager = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize Collections for ComboBoxes
            FilterTypes = new ObservableCollection<string> { "EMA", "SMA", "None" };
            LogFormats = new ObservableCollection<string> { "CSV", "JSON" };
            CalibrationModes = new ObservableCollection<string> { "Regression", "Piecewise" };
            
            // Commands
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            OpenDataDirectoryCommand = new RelayCommand(_ => OpenDataDirectory());
            OpenSettingsFileCommand = new RelayCommand(_ => OpenSettingsFile());
            ResetCalibrationInternalCommand = new RelayCommand(_ => ResetCalibration(0, "Internal ADC"));
            ResetCalibrationADS1115Command = new RelayCommand(_ => ResetCalibration(1, "ADS1115"));
            
            RefreshCalibrationData();
        }

        // Collections
        public ObservableCollection<string> FilterTypes { get; }
        public ObservableCollection<string> LogFormats { get; }
        public ObservableCollection<string> CalibrationModes { get; }

        // --- Proxy Properties to SettingsManager ---

        // Weight Filtering
        public string FilterType
        {
            get => _settingsManager.Settings.FilterType;
            set
            {
                if (_settingsManager.Settings.FilterType != value)
                {
                    _settingsManager.Settings.FilterType = value;
                    OnPropertyChanged();
                    SaveFilterSettings();
                }
            }
        }

        public double FilterAlpha
        {
            get => _settingsManager.Settings.FilterAlpha;
            set
            {
                if (Math.Abs(_settingsManager.Settings.FilterAlpha - value) > 0.001)
                {
                    _settingsManager.Settings.FilterAlpha = value;
                    OnPropertyChanged();
                    SaveFilterSettings();
                }
            }
        }

        public int FilterWindowSize
        {
            get => _settingsManager.Settings.FilterWindowSize;
            set
            {
                if (_settingsManager.Settings.FilterWindowSize != value)
                {
                    _settingsManager.Settings.FilterWindowSize = value;
                    OnPropertyChanged();
                    SaveFilterSettings();
                }
            }
        }

        public bool FilterEnabled
        {
            get => _settingsManager.Settings.FilterEnabled;
            set
            {
                if (_settingsManager.Settings.FilterEnabled != value)
                {
                    _settingsManager.Settings.FilterEnabled = value;
                    OnPropertyChanged();
                    SaveFilterSettings();
                }
            }
        }

        // Display Settings
        public int WeightDisplayDecimals
        {
            get => _settingsManager.Settings.WeightDisplayDecimals;
            set
            {
                if (_settingsManager.Settings.WeightDisplayDecimals != value)
                {
                    _settingsManager.Settings.WeightDisplayDecimals = value;
                    OnPropertyChanged();
                    SaveDisplaySettings();
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
                    SaveDisplaySettings();
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
                    SaveDisplaySettings();
                }
            }
        }

        // UI Visibility
        public bool ShowRawADC
        {
            get => _settingsManager.Settings.ShowRawADC;
            set
            {
                if (_settingsManager.Settings.ShowRawADC != value)
                {
                    _settingsManager.Settings.ShowRawADC = value;
                    OnPropertyChanged();
                    SaveVisibilitySettings();
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
                    SaveVisibilitySettings();
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
                    SaveVisibilitySettings();
                }
            }
        }

        // Advanced Settings
        public string LogFileFormat
        {
            get => _settingsManager.Settings.LogFileFormat;
            set
            {
                if (_settingsManager.Settings.LogFileFormat != value)
                {
                    _settingsManager.Settings.LogFileFormat = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
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
                    SaveAdvancedSettings();
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
                    SaveAdvancedSettings();
                }
            }
        }
        
        public bool ShowCalibrationQualityMetrics
        {
            get => _settingsManager.Settings.ShowCalibrationQualityMetrics;
            set
            {
                if (_settingsManager.Settings.ShowCalibrationQualityMetrics != value)
                {
                    _settingsManager.Settings.ShowCalibrationQualityMetrics = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }
        
        // Calibration Mode Settings
        public string CalibrationMode
        {
            get => _settingsManager.Settings.CalibrationMode;
            set
            {
                if (_settingsManager.Settings.CalibrationMode != value)
                {
                    _settingsManager.Settings.CalibrationMode = value;
                    OnPropertyChanged();
                    SaveCalibrationMode();
                }
            }
        }

        // Advanced Calibration Settings
        public bool CalibrationAveragingEnabled
        {
            get => _settingsManager.Settings.CalibrationAveragingEnabled;
            set
            {
                if (_settingsManager.Settings.CalibrationAveragingEnabled != value)
                {
                    _settingsManager.Settings.CalibrationAveragingEnabled = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public int CalibrationSampleCount
        {
            get => _settingsManager.Settings.CalibrationSampleCount;
            set
            {
                if (_settingsManager.Settings.CalibrationSampleCount != value)
                {
                    _settingsManager.Settings.CalibrationSampleCount = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public int CalibrationCaptureDurationMs
        {
            get => _settingsManager.Settings.CalibrationCaptureDurationMs;
            set
            {
                if (_settingsManager.Settings.CalibrationCaptureDurationMs != value)
                {
                    _settingsManager.Settings.CalibrationCaptureDurationMs = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public bool CalibrationUseMedian
        {
            get => _settingsManager.Settings.CalibrationUseMedian;
            set
            {
                if (_settingsManager.Settings.CalibrationUseMedian != value)
                {
                    _settingsManager.Settings.CalibrationUseMedian = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public bool CalibrationRemoveOutliers
        {
            get => _settingsManager.Settings.CalibrationRemoveOutliers;
            set
            {
                if (_settingsManager.Settings.CalibrationRemoveOutliers != value)
                {
                    _settingsManager.Settings.CalibrationRemoveOutliers = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public double CalibrationOutlierThreshold
        {
            get => _settingsManager.Settings.CalibrationOutlierThreshold;
            set
            {
                if (Math.Abs(_settingsManager.Settings.CalibrationOutlierThreshold - value) > 0.001)
                {
                    _settingsManager.Settings.CalibrationOutlierThreshold = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        public double CalibrationMaxStdDev
        {
            get => _settingsManager.Settings.CalibrationMaxStdDev;
            set
            {
                if (Math.Abs(_settingsManager.Settings.CalibrationMaxStdDev - value) > 0.001)
                {
                    _settingsManager.Settings.CalibrationMaxStdDev = value;
                    OnPropertyChanged();
                    SaveAdvancedSettings();
                }
            }
        }

        // --- Calibration Data Display ---
        private LinearCalibration? _internalCal;
        private LinearCalibration? _adsCal;

        public string InternalCalStatus => _internalCal?.IsValid == true ? "✓ Valid" : "⚠ Not Calibrated";
        public string InternalCalSlope => _internalCal?.Slope.ToString("F6") ?? "N/A";
        public string InternalCalIntercept => _internalCal?.Intercept.ToString("F6") ?? "N/A";
        public string InternalCalDate => _internalCal?.CalibrationDate.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

        public string AdsCalStatus => _adsCal?.IsValid == true ? "✓ Valid" : "⚠ Not Calibrated";
        public string AdsCalSlope => _adsCal?.Slope.ToString("F6") ?? "N/A";
        public string AdsCalIntercept => _adsCal?.Intercept.ToString("F6") ?? "N/A";
        public string AdsCalDate => _adsCal?.CalibrationDate.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        
        // Save Directory Info
        public string SaveDirectory => _settingsManager.Settings.SaveDirectory;
        
        public string DataStatsText
        {
            get
            {
                try
                {
                    if (Directory.Exists(SaveDirectory))
                    {
                        var files = Directory.GetFiles(SaveDirectory, "*.csv");
                        return $"{files.Length} CSV files";
                    }
                    return "Directory not found";
                }
                catch
                {
                    return "Error reading directory";
                }
            }
        }

        // --- Commands ---

        public ICommand SaveSettingsCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand OpenSettingsFileCommand { get; }
        public ICommand ResetCalibrationInternalCommand { get; }
        public ICommand ResetCalibrationADS1115Command { get; }

        private void SaveSettings()
        {
            _settingsManager.SaveSettings();
        }

        private void SaveFilterSettings()
        {
            // Assuming SettingsManager exposes specific update methods or we just save all
            // Ideally we'd call _settingsManager.SetFilterSettings(...) but since we're modifying the
            // internal object directly via properties above, calling SaveSettings() is sufficient 
            // if we want to persist immediately.
            _settingsManager.SaveSettings();
        }
        
        private void SaveDisplaySettings() => _settingsManager.SaveSettings();
        private void SaveVisibilitySettings() => _settingsManager.SaveSettings();
        private void SaveAdvancedSettings() => _settingsManager.SaveSettings();
        private void SaveCalibrationMode() => _settingsManager.SaveSettings();

        private void OpenDataDirectory()
        {
            try
            {
                if (Directory.Exists(SaveDirectory))
                {
                    Process.Start("explorer.exe", SaveDirectory);
                }
                else
                {
                    MessageBox.Show($"Directory not found: {SaveDirectory}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSettingsFile()
        {
            try
            {
                var path = PathHelper.GetSettingsPath();
                if (File.Exists(path))
                {
                    Process.Start("notepad.exe", path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening settings file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetCalibration(byte mode, string text)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the calibration for {text}?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                LinearCalibration.DeleteCalibration(mode);
                RefreshCalibrationData(); // Reload data
                MessageBox.Show("Calibration deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RefreshCalibrationData()
        {
             _internalCal = LinearCalibration.LoadFromFile(0);
             _adsCal = LinearCalibration.LoadFromFile(1);
             OnPropertyChanged(nameof(InternalCalStatus));
             OnPropertyChanged(nameof(InternalCalSlope));
             OnPropertyChanged(nameof(InternalCalIntercept));
             OnPropertyChanged(nameof(InternalCalDate));
             OnPropertyChanged(nameof(AdsCalStatus));
             OnPropertyChanged(nameof(AdsCalSlope));
             OnPropertyChanged(nameof(AdsCalIntercept));
             OnPropertyChanged(nameof(AdsCalDate));
        }
    }
}
