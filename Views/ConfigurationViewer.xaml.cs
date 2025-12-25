using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Text.Json;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class ConfigurationViewer : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly LinearCalibration? _totalCalibrationInternal;
        private readonly LinearCalibration? _totalCalibrationADS1115;
        private readonly TareManager _tareManager;

        public ConfigurationViewer()
        {
            InitializeComponent();
            
            _settingsManager = SettingsManager.Instance;
            // Load calibrations for both ADC modes (total weight)
            _totalCalibrationInternal = LinearCalibration.LoadFromFile(0);
            _totalCalibrationADS1115 = LinearCalibration.LoadFromFile(1);
            _tareManager = new TareManager();
            _tareManager.LoadFromFile();

            LoadConfigurationData();
        }

        private void LoadConfigurationData()
        {
            try
            {
                LoadApplicationSettings();
                LoadCalibrationData();
                LoadTareData();
                LoadDataDirectoryInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration data: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadApplicationSettings()
        {
            var settings = _settingsManager.Settings;
            var settingsPath = PathHelper.GetSettingsPath(); // Portable: next to executable

            SettingsFileLocation.Text = settingsPath;
            SettingsComPort.Text = settings.ComPort;
            SettingsTransmissionRate.Text = GetRateText(settings.TransmissionRate);
            SettingsSaveDirectory.Text = settings.SaveDirectory;
            SettingsAdcMode.Text = settings.LastKnownADCMode == 0 ? "Internal ADC" : "ADS1115";
            SettingsLastSaved.Text = settings.LastSaved.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void LoadCalibrationData()
        {
            // Load Internal ADC calibration (total weight)
            LoadCalibrationForMode(0, _totalCalibrationInternal, 
                LeftCalFileLocationInternal, LeftCalStatusInternal, LeftCalSlopeInternal, 
                LeftCalInterceptInternal, LeftCalZeroPointInternal, LeftCalKnownWeightInternal, 
                LeftCalCalibratedInternal);
            
            // Load ADS1115 calibration (total weight)
            LoadCalibrationForMode(1, _totalCalibrationADS1115, 
                LeftCalFileLocationADS1115, LeftCalStatusADS1115, LeftCalSlopeADS1115, 
                LeftCalInterceptADS1115, LeftCalZeroPointADS1115, LeftCalKnownWeightADS1115, 
                LeftCalCalibratedADS1115);
        }
        
        private void LoadCalibrationForMode(byte adcMode, LinearCalibration? calibration,
            System.Windows.Controls.TextBlock fileLocation, System.Windows.Controls.TextBlock status,
            System.Windows.Controls.TextBlock slope, System.Windows.Controls.TextBlock intercept,
            System.Windows.Controls.TextBlock zeroPoint, System.Windows.Controls.TextBlock knownWeight,
            System.Windows.Controls.TextBlock calibrated)
        {
            string modeText = adcMode == 0 ? "Internal ADC" : "ADS1115";
            var calPath = PathHelper.GetCalibrationPath(adcMode);
            fileLocation.Text = calPath;
            
            if (calibration != null && calibration.IsValid)
            {
                status.Text = $"✓ Valid";
                slope.Text = calibration.Slope.ToString("F6");
                intercept.Text = calibration.Intercept.ToString("F6");
                
                if (calibration.Points != null && calibration.Points.Count > 0)
                {
                    var firstPoint = calibration.Points.First();
                    var lastPoint = calibration.Points.Last();
                    
                    // Format as signed for ADS1115, unsigned for Internal
                    if (adcMode == 1) // ADS1115
                    {
                        zeroPoint.Text = firstPoint.RawADC >= 0 ? $"+{firstPoint.RawADC}" : firstPoint.RawADC.ToString();
                        knownWeight.Text = lastPoint.RawADC >= 0 ? $"+{lastPoint.RawADC}" : lastPoint.RawADC.ToString();
                    }
                    else // Internal
                    {
                        zeroPoint.Text = firstPoint.RawADC.ToString();
                        knownWeight.Text = lastPoint.RawADC.ToString();
                    }
                }
                else
                {
                    zeroPoint.Text = "N/A";
                    knownWeight.Text = "N/A";
                }
                calibrated.Text = calibration.CalibrationDate.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                status.Text = "⚠ Not Calibrated";
                slope.Text = "N/A";
                intercept.Text = "N/A";
                zeroPoint.Text = "N/A";
                knownWeight.Text = "N/A";
                calibrated.Text = "N/A";
            }
        }

        private void LoadTareData()
        {
            var tarePath = Path.Combine(_settingsManager.Settings.SaveDirectory, "tare_config.json");
            TareFileLocation.Text = tarePath;
            
            // Show tare status for both ADC modes (total weight)
            bool internalTared = _tareManager.IsTared(0);
            bool ads1115Tared = _tareManager.IsTared(1);
            
            // Show status for both modes
            if (internalTared || ads1115Tared)
            {
                string status = "✓ Tared (";
                if (internalTared) status += "Internal";
                if (internalTared && ads1115Tared) status += ", ";
                if (ads1115Tared) status += "ADS1115";
                status += ")";
                TareLeftStatus.Text = status;
                
                // Show offset for Internal mode (or ADS1115 if Internal not tared)
                double offset = internalTared 
                    ? _tareManager.GetOffsetKg(0)
                    : (ads1115Tared ? _tareManager.GetOffsetKg(1) : 0);
                TareLeftBaseline.Text = offset.ToString("F3") + " kg";
            }
            else
            {
                TareLeftStatus.Text = "⚠ Not Tared";
                TareLeftBaseline.Text = "0.000 kg";
            }
            
            // Show most recent tare time
            DateTime internalTime = _tareManager.GetTareTime(0);
            DateTime ads1115Time = _tareManager.GetTareTime(1);
            
            DateTime mostRecent = DateTime.MinValue;
            if (internalTared && internalTime > mostRecent) mostRecent = internalTime;
            if (ads1115Tared && ads1115Time > mostRecent) mostRecent = ads1115Time;
            
            if (mostRecent != DateTime.MinValue)
            {
                TareLastUpdated.Text = mostRecent.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                TareLastUpdated.Text = "Never";
            }
        }

        private void LoadDataDirectoryInfo()
        {
            var dataDir = _settingsManager.Settings.SaveDirectory;
            DataDirectoryPath.Text = dataDir;
            
            try
            {
                if (Directory.Exists(dataDir))
                {
                    var csvFiles = Directory.GetFiles(dataDir, "*.csv");
                    DataCsvFiles.Text = $"{csvFiles.Length} files";
                    
                    long totalSize = 0;
                    foreach (var file in csvFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    
                    DataTotalSize.Text = FormatFileSize(totalSize);
                }
                else
                {
                    DataCsvFiles.Text = "Directory not found";
                    DataTotalSize.Text = "N/A";
                }
            }
            catch (Exception ex)
            {
                DataCsvFiles.Text = $"Error: {ex.Message}";
                DataTotalSize.Text = "N/A";
            }
        }

        private string GetRateText(byte rate)
        {
            return rate switch
            {
                0x01 => "100Hz",
                0x02 => "500Hz", 
                0x03 => "1kHz",
                0x05 => "1Hz",
                _ => "Unknown"
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void OpenSettingsFileBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsPath = PathHelper.GetSettingsPath(); // Portable: next to executable
                
                if (File.Exists(settingsPath))
                {
                    Process.Start("notepad.exe", settingsPath);
                }
                else
                {
                    MessageBox.Show("Settings file not found.", "File Not Found", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening settings file: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLeftCalFileBtnInternal_Click(object sender, RoutedEventArgs e)
        {
            var calPath = PathHelper.GetCalibrationPath(0);
            OpenConfigFile(Path.GetFileName(calPath));
        }

        private void OpenRightCalFileBtnInternal_Click(object sender, RoutedEventArgs e)
        {
            OpenConfigFile("calibration_right_internal.json");
        }
        
        private void OpenLeftCalFileBtnADS1115_Click(object sender, RoutedEventArgs e)
        {
            var calPath = PathHelper.GetCalibrationPath(1);
            OpenConfigFile(Path.GetFileName(calPath));
        }
        
        private void ResetLeftCalInternal_Click(object sender, RoutedEventArgs e)
        {
            ResetCalibration(0, "Internal ADC");
        }
        
        private void ResetLeftCalADS1115_Click(object sender, RoutedEventArgs e)
        {
            ResetCalibration(1, "ADS1115");
        }
        
        private void ResetCalibration(byte adcMode, string modeName)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the total weight calibration for {modeName}?\n\n" +
                    "This will allow you to recalibrate for this ADC mode.",
                    "Confirm Reset Calibration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    LinearCalibration.DeleteCalibration(adcMode);
                    MessageBox.Show(
                        $"Total weight calibration for {modeName} has been deleted.\n\n" +
                        "You can now calibrate again.",
                        "Calibration Reset",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // Reload configuration data
                    LoadConfigurationData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting calibration: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTareFileBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenConfigFile("tare_config.json");
        }

        private void OpenConfigFile(string filename)
        {
            try
            {
                // For calibration files, use PathHelper to get correct path
                string filePath;
                if (filename.StartsWith("calibration_"))
                {
                    // Extract mode from filename
                    var parts = filename.Replace(".json", "").Split('_');
                    if (parts.Length >= 3 && parts[1] == "total")
                    {
                        string modeStr = parts[2]; // "internal" or "ads1115"
                        byte adcMode = modeStr == "internal" ? (byte)0 : (byte)1;
                        filePath = PathHelper.GetCalibrationPath(adcMode);
                    }
                    else
                    {
                        filePath = Path.Combine(_settingsManager.Settings.SaveDirectory, filename);
                    }
                }
                else
                {
                    filePath = Path.Combine(_settingsManager.Settings.SaveDirectory, filename);
                }
                
                if (File.Exists(filePath))
                {
                    Process.Start("notepad.exe", filePath);
                }
                else
                {
                    MessageBox.Show($"{filename} not found.", "File Not Found", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening {filename}: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenDataDirectoryBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataDir = _settingsManager.Settings.SaveDirectory;
                
                if (Directory.Exists(dataDir))
                {
                    Process.Start("explorer.exe", dataDir);
                }
                else
                {
                    MessageBox.Show("Data directory not found.", "Directory Not Found", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening data directory: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigurationData();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
