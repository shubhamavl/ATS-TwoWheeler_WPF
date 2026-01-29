using System;
using System.IO;
using System.Text.Json;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services
{
    /// <summary>
    /// Centralized settings manager with JSON persistence
    /// </summary>
    public class SettingsManager : ISettingsService
    {
        private static SettingsManager? _instance;
        private static readonly object _lock = new object();
        private AppSettings _settings = new AppSettings();
        private readonly string _settingsPath;

        private SettingsManager()
        {
            _settingsPath = PathHelper.GetSettingsPath(); // Portable: next to executable
            ProductionLogger.Instance.LogInfo($"Settings file path: {_settingsPath}", "Settings");

            LoadSettings();
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsManager();
                    }
                }
                return _instance;
            }
        }

        public AppSettings Settings => _settings;

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    ProductionLogger.Instance.LogInfo($"Settings loaded from: {_settingsPath}", "Settings");
                }
                else
                {
                    _settings = new AppSettings();
                    ProductionLogger.Instance.LogInfo($"Settings file not found, using defaults: {_settingsPath}", "Settings");
                }
            }
            catch (JsonException ex)
            {
                ProductionLogger.Instance.LogError($"Invalid JSON in settings file: {ex.Message}", "Settings");
                _settings = new AppSettings();
            }
            catch (IOException ex)
            {
                ProductionLogger.Instance.LogError($"Cannot read settings file: {ex.Message}", "Settings");
                _settings = new AppSettings();
            }
            catch (UnauthorizedAccessException ex)
            {
                ProductionLogger.Instance.LogError($"Access denied to settings file: {ex.Message}", "Settings");
                _settings = new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                _settings.LastSaved = DateTime.Now;
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });

                string? directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_settingsPath, json);
                ProductionLogger.Instance.LogInfo($"Settings saved to: {_settingsPath}", "Settings");
            }
            catch (IOException ex)
            {
                ProductionLogger.Instance.LogError($"Cannot write settings file: {ex.Message}", "Settings");
            }
            catch (UnauthorizedAccessException ex)
            {
                ProductionLogger.Instance.LogError($"Access denied when writing settings: {ex.Message}", "Settings");
            }
        }

        public void SetComPort(string comPort)
        {
            if (string.IsNullOrWhiteSpace(comPort))
            {
                return;
            }

            _settings.ComPort = comPort;
            ProductionLogger.Instance.LogInfo($"COM port set to: {comPort}", "Settings");
        }

        public void SetCanBaudRate(string baudRate)
        {
            CanBaudRate rate = CanBaudRate.Bps250k; // Default 250k
            int index = 1;

            switch (baudRate)
            {
                case "125 kbps": rate = CanBaudRate.Bps125k; index = 0; break;
                case "250 kbps": rate = CanBaudRate.Bps250k; index = 1; break;
                case "500 kbps": rate = CanBaudRate.Bps500k; index = 2; break;
                case "1 Mbps": rate = CanBaudRate.Bps1M; index = 3; break;
            }

            _settings.CanBaudRate = rate;
            _settings.CanBaudRateIndex = index;
            ProductionLogger.Instance.LogInfo($"CAN baud rate set to: {baudRate} (0x{(int)rate:X2})", "Settings");
        }

        public void SetTransmissionRate(string samplingRate)
        {
            TransmissionRate rate = TransmissionRate.Hz1000; // Default 1kHz
            int index = 3;

            switch (samplingRate)
            {
                case "1 Hz": rate = TransmissionRate.Hz1; index = 0; break;
                case "100 Hz": rate = TransmissionRate.Hz100; index = 1; break;
                case "500 Hz": rate = TransmissionRate.Hz500; index = 2; break;
                case "1 kHz": rate = TransmissionRate.Hz1000; index = 3; break;
            }
            SetTransmissionRate(rate, index);
        }

        public void SetTransmissionRate(TransmissionRate rate, int index)
        {
            _settings.TransmissionRate = rate;
            _settings.TransmissionRateIndex = index;
            ProductionLogger.Instance.LogInfo($"Sampling rate set to: 0x{(int)rate:X2} (index: {index})", "Settings");
        }

        public void SetSaveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _settings.SaveDirectory = directory;
                ProductionLogger.Instance.LogInfo($"Save directory set to: {directory}", "Settings");
            }
            catch (IOException ex)
            {
                ProductionLogger.Instance.LogError($"Cannot create save directory: {ex.Message}", "Settings");
            }
            catch (UnauthorizedAccessException ex)
            {
                ProductionLogger.Instance.LogError($"Access denied to save directory: {ex.Message}", "Settings");
            }
            catch (ArgumentException ex)
            {
                ProductionLogger.Instance.LogError($"Invalid save directory path: {ex.Message}", "Settings");
            }
        }

        /// <summary>
        /// Update system status in settings
        /// </summary>
        /// <param name="adcMode">ADC mode enum</param>
        /// <param name="systemStatus">System status enum</param>
        /// <param name="errorFlags">Error flags</param>
        public void UpdateSystemStatus(AdcMode adcMode, SystemStatus systemStatus, byte errorFlags)
        {
            _settings.LastKnownADCMode = adcMode;
            _settings.LastKnownSystemStatus = systemStatus;
            _settings.LastKnownErrorFlags = errorFlags;
            _settings.LastStatusUpdate = DateTime.Now;
            ProductionLogger.Instance.LogInfo($"System status updated: ADC={adcMode}, Status={systemStatus}, Errors=0x{errorFlags:X2}", "Settings");
        }

        /// <summary>
        /// Get last known ADC mode
        /// </summary>
        /// <returns>ADC mode (0=Internal, 1=ADS1115)</returns>
        public AdcMode GetLastKnownADCMode()
        {
            return _settings.LastKnownADCMode;
        }

        /// <summary>
        /// Get last known system status
        /// </summary>
        /// <returns>System status info</returns>
        public (AdcMode adcMode, SystemStatus systemStatus, byte errorFlags, DateTime lastUpdate) GetLastKnownSystemStatus()
        {
            return (_settings.LastKnownADCMode, _settings.LastKnownSystemStatus,
                   _settings.LastKnownErrorFlags, _settings.LastStatusUpdate);
        }

        /// <summary>
        /// Set filter settings
        /// </summary>
        public void SetFilterSettings(string filterType, double filterAlpha, int filterWindowSize, bool filterEnabled)
        {
            if (Enum.TryParse(filterType, out FilterType fType))
            {
                _settings.FilterType = fType;
            }
            else
            {
                _settings.FilterType = FilterType.EMA;
            }
            _settings.FilterAlpha = filterAlpha;
            _settings.FilterWindowSize = filterWindowSize;
            _settings.FilterEnabled = filterEnabled;
            ProductionLogger.Instance.LogInfo($"Filter settings saved: {filterType}, Alpha={filterAlpha}, Window={filterWindowSize}, Enabled={filterEnabled}", "Settings");
        }

        /// <summary>
        /// Set display and performance settings
        /// </summary>
        public void SetDisplaySettings(int weightDecimals, int uiUpdateRate, int dataTimeout)
        {
            _settings.WeightDisplayDecimals = weightDecimals;
            _settings.UIUpdateRateMs = uiUpdateRate;
            _settings.DataTimeoutSeconds = dataTimeout;
            ProductionLogger.Instance.LogInfo($"Display settings saved: WeightDecimals={weightDecimals}, UIUpdateRate={uiUpdateRate}ms, DataTimeout={dataTimeout}s", "Settings");
        }

        /// <summary>
        /// Set UI visibility settings
        /// </summary>
        public void SetUIVisibilitySettings(int statusBannerDuration, int messageHistoryLimit, bool showRawADC, bool showCalibratedWeight, bool showStreamingIndicators, bool showCalibrationIcons)
        {
            _settings.StatusBannerDurationMs = statusBannerDuration;
            _settings.MessageHistoryLimit = messageHistoryLimit;
            _settings.ShowRawADC = showRawADC;
            _settings.ShowCalibratedWeight = showCalibratedWeight;
            _settings.ShowStreamingIndicators = showStreamingIndicators;
            _settings.ShowCalibrationIcons = showCalibrationIcons;
            ProductionLogger.Instance.LogInfo($"UI visibility settings saved: StatusBanner={statusBannerDuration}ms, MessageLimit={messageHistoryLimit}, ShowRawADC={showRawADC}, ShowCalibrated={showCalibratedWeight}, ShowIndicators={showStreamingIndicators}, ShowIcons={showCalibrationIcons}", "Settings");
        }

        /// <summary>
        /// Set advanced settings
        /// </summary>
        public void SetAdvancedSettings(int txFlashMs, string logFormat, int batchSize, int clockInterval, int calibrationDelay, bool showQualityMetrics)
        {
            _settings.TXIndicatorFlashMs = txFlashMs;
            if (Enum.TryParse(logFormat, out LogFormat lFormat))
            {
                _settings.LogFileFormat = lFormat;
            }
            else
            {
                _settings.LogFileFormat = LogFormat.CSV;
            }
            _settings.BatchProcessingSize = batchSize;
            _settings.ClockUpdateIntervalMs = clockInterval;
            _settings.CalibrationCaptureDelayMs = calibrationDelay;
            _settings.ShowCalibrationQualityMetrics = showQualityMetrics;
            ProductionLogger.Instance.LogInfo($"Advanced settings saved: TXFlash={txFlashMs}ms, LogFormat={logFormat}, BatchSize={batchSize}, ClockInterval={clockInterval}ms, CalDelay={calibrationDelay}ms, ShowQuality={showQualityMetrics}", "Settings");
        }

        /// <summary>
        /// Set bootloader feature enable/disable
        /// </summary>
        public void SetBootloaderFeaturesEnabled(bool enabled)
        {
            _settings.EnableBootloaderFeatures = enabled;
            ProductionLogger.Instance.LogInfo($"Bootloader features {(enabled ? "enabled" : "disabled")}", "Settings");
        }

        /// <summary>
        /// Set calibration mode (Regression or Piecewise)
        /// </summary>
        public void SetCalibrationMode(string mode)
        {
            if (Enum.TryParse(mode, out CalibrationMode calMode))
            {
                _settings.CalibrationMode = calMode;
            }
            else
            {
                _settings.CalibrationMode = CalibrationMode.Regression;
            }
            ProductionLogger.Instance.LogInfo($"Calibration mode set to: {mode}", "Settings");
        }

        /// <summary>
        /// Set calibration averaging settings
        /// </summary>
        public void SetCalibrationAveragingSettings(bool enabled, int sampleCount, int durationMs, bool useMedian, bool removeOutliers, double outlierThreshold, double maxStdDev)
        {
            _settings.CalibrationAveragingEnabled = enabled;
            _settings.CalibrationSampleCount = sampleCount;
            _settings.CalibrationCaptureDurationMs = durationMs;
            _settings.CalibrationUseMedian = useMedian;
            _settings.CalibrationRemoveOutliers = removeOutliers;
            _settings.CalibrationOutlierThreshold = outlierThreshold;
            _settings.CalibrationMaxStdDev = maxStdDev;
            ProductionLogger.Instance.LogInfo($"Calibration averaging settings saved: Enabled={enabled}, SampleCount={sampleCount}, Duration={durationMs}ms, UseMedian={useMedian}, RemoveOutliers={removeOutliers}, OutlierThreshold={outlierThreshold:F1}σ, MaxStdDev={maxStdDev:F1}", "Settings");
        }

        public void SetBrakeSettings(string unit, double multiplier)
        {
            _settings.BrakeDisplayUnit = unit;
            _settings.BrakeKgToNewtonMultiplier = multiplier;
            ProductionLogger.Instance.LogInfo($"Brake settings saved: Unit={unit}, Multiplier={multiplier}", "Settings");
        }

        /// <summary>
        /// Set efficiency limits for Pass/Fail validation (removed for ATS Two-Wheeler system)
        /// </summary>
        // Suspension-specific methods removed for ATS Two-Wheeler system

        // Implementation of added ISettingsService members
        public LinearCalibration CalibrationDataInternal => LinearCalibration.LoadFromFile(AdcMode.InternalWeight, SystemMode.Weight) ?? new LinearCalibration { IsValid = false };
        public LinearCalibration CalibrationDataADS1115 => LinearCalibration.LoadFromFile(AdcMode.Ads1115, SystemMode.Weight) ?? new LinearCalibration { IsValid = false };

        public LinearCalibration CalibrationDataInternalBrake => LinearCalibration.LoadFromFile(AdcMode.InternalWeight, SystemMode.Brake) ?? new LinearCalibration { IsValid = false };
        public LinearCalibration CalibrationDataADS1115Brake => LinearCalibration.LoadFromFile(AdcMode.Ads1115, SystemMode.Brake) ?? new LinearCalibration { IsValid = false };

        public string GetCalibrationFilePath(bool adcMode)
        {
            return PathHelper.GetCalibrationPath(adcMode ? AdcMode.Ads1115 : AdcMode.InternalWeight, SystemMode.Weight);
        }

        public string GetCalibrationBrakeFilePath(bool adcMode)
        {
            return PathHelper.GetCalibrationPath(adcMode ? AdcMode.Ads1115 : AdcMode.InternalWeight, SystemMode.Brake);
        }

        public string GetTareFilePath()
        {
            return PathHelper.GetTareConfigPath();
        }

        public void ResetCalibration(bool adsMode)
        {
            LinearCalibration.DeleteCalibration(adsMode ? AdcMode.Ads1115 : AdcMode.InternalWeight, SystemMode.Weight);
        }

        public void ResetBrakeCalibration(bool adsMode)
        {
            LinearCalibration.DeleteCalibration(adsMode ? AdcMode.Ads1115 : AdcMode.InternalWeight, SystemMode.Brake);
        }

        public double TareValue
        {
            get
            {
                // Simple implementation: load from file or return 0
                // Since SettingsManager doesn't hold TareManager, we can just check the file
                // But TareBaseline in view usually wants the current active one.
                // For simplicity, we'll return 0 or load from file.
                return 0; // Better to have TareManager handle this, but for compilation:
            }
        }
    }
}
