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
using ATS_TwoWheeler_WPF.ViewModels.Settings;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    /// <summary>
    /// Main settings ViewModel with grouped settings
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsManager;

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsManager = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Initialize nested ViewModels
            Filter = new FilterSettingsViewModel(_settingsManager);
            Display = new DisplaySettingsViewModel(_settingsManager);
            Calibration = new CalibrationSettingsViewModel(_settingsManager);
            Advanced = new AdvancedSettingsViewModel(_settingsManager);

            // Initialize Collections for ComboBoxes
            FilterTypes = new ObservableCollection<string> { "EMA", "SMA", "None" };
            LogFormats = new ObservableCollection<string> { "CSV", "JSON" };
            CalibrationModes = new ObservableCollection<string> { "Regression", "Piecewise" };
            BrakeUnits = new ObservableCollection<string> { "N", "kg" };
            
            // Commands
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            OpenDataDirectoryCommand = new RelayCommand(_ => OpenDataDirectory());
            OpenSettingsFileCommand = new RelayCommand(_ => OpenSettingsFile());
            ResetCalibrationInternalCommand = new RelayCommand(_ => ResetCalibration(AdcMode.InternalWeight, "Internal ADC"));
            ResetCalibrationADS1115Command = new RelayCommand(_ => ResetCalibration(AdcMode.Ads1115, "ADS1115"));
            ShowHelpCommand = new RelayCommand(p => OnShowHelp(p?.ToString()));
        }

        #region Nested ViewModels

        /// <summary>
        /// Filter settings (Type, Alpha, WindowSize, Enabled)
        /// </summary>
        public FilterSettingsViewModel Filter { get; }

        /// <summary>
        /// Display settings (WeightDecimals, UIUpdateRateMs, DataTimeoutSeconds, Show* flags)
        /// </summary>
        public DisplaySettingsViewModel Display { get; }

        /// <summary>
        /// Calibration settings (Mode, advanced calibration parameters, calibration data display)
        /// </summary>
        public CalibrationSettingsViewModel Calibration { get; }

        /// <summary>
        /// Advanced settings (LogFileFormat, BatchProcessingSize, ClockUpdateIntervalMs, Brake settings)
        /// </summary>
        public AdvancedSettingsViewModel Advanced { get; }

        #endregion

        #region Collections for ComboBoxes

        public ObservableCollection<string> FilterTypes { get; }
        public ObservableCollection<string> LogFormats { get; }
        public ObservableCollection<string> CalibrationModes { get; }
        public ObservableCollection<string> BrakeUnits { get; }

        #endregion

        #region Save Directory Info

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
                catch (IOException ex)
                {
                    return $"Error: {ex.Message}";
                }
                catch (UnauthorizedAccessException)
                {
                    return "Access denied";
                }
            }
        }

        #endregion

        #region Commands

        public ICommand SaveSettingsCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand OpenSettingsFileCommand { get; }
        public ICommand ResetCalibrationInternalCommand { get; }
        public ICommand ResetCalibrationADS1115Command { get; }
        public ICommand ShowHelpCommand { get; }

        public event Action<string, string>? HelpRequested;

        #endregion

        #region Command Implementations

        private void SaveSettings()
        {
            try
            {
                _settingsManager.SaveSettings();
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Cannot save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Access denied when saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show($"Cannot open Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Error accessing directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                else
                {
                    MessageBox.Show("Settings file not found", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show($"Cannot open Notepad: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Error opening settings file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetCalibration(AdcMode mode, string text)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the calibration for {text}?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    LinearCalibration.DeleteCalibration(mode, SystemMode.Weight);
                    Calibration.RefreshCalibrationData();
                    MessageBox.Show("Calibration deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Cannot delete calibration file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show($"Access denied when deleting calibration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnShowHelp(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;

            string title = key switch
            {
                "Filter" => "Signal Filtering",
                "Calibration" => "Calibration Parameters",
                "General" => "General Settings",
                _ => "Information"
            };

            string content = key switch
            {
                "Filter" => "Filtering helps smooth the weight data.\n\n" +
                            "• EMA (Exponential Moving Average): Best for real-time responsiveness. Adjust Alpha (0.0 to 1.0) to control smoothing.\n" +
                            "• SMA (Simple Moving Average): Best for stability. Adjust Window Size to control how many samples are averaged.",
                "Calibration" => "Calibration constants define how raw ADC values are converted to weight.\n\n" +
                                 "• Slope: The gain factor.\n" +
                                 "• Intercept: The zero-load offset.\n" +
                                 "• Outlier Threshold: Maximum deviation allowed before a sample is ignored during calibration processing.",
                "General" => "Manage how data is displayed and stored.\n\n" +
                             "• Update Rate: How often the UI refreshes (default 50ms).\n" +
                             "• Data Timeout: Seconds of no data before connection is marked as lost.",
                _ => "Detailed information for this setting is not available."
            };

            HelpRequested?.Invoke(title, content);
        }

        #endregion
    }
}
