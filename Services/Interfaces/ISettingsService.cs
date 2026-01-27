using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }

        void LoadSettings();
        void SaveSettings();
        
        void SetComPort(string portName);
        void SetCanBaudRate(string baudRate);
        void SetTransmissionRate(string samplingRate);
        void SetSaveDirectory(string path);
        
        void UpdateSystemStatus(AdcMode adcMode, SystemStatus systemStatus, byte errorFlags);
        AdcMode GetLastKnownADCMode();
        
        void SetFilterSettings(string type, double alpha, int windowSize, bool enabled);
        void SetDisplaySettings(int weightDecimals, int uiUpdateRate, int dataTimeoutSeconds);
        void SetUIVisibilitySettings(int bannerDuration, int messageLimit, bool showRawADC, bool showCalibrated, bool showStreaming, bool showCalibrationIcons);
        void SetAdvancedSettings(int txFlashMs, string logFormat, int batchSize, int clockInterval, int calibrationDelay, bool showQualityMetrics);
        void SetBootloaderFeaturesEnabled(bool enabled);
        void SetCalibrationMode(string mode);
        void SetCalibrationAveragingSettings(bool enabled, int sampleCount, int durationMs, bool useMedian, bool removeOutliers, double outlierThreshold, double maxStdDev);
        
        // Calibration and Tare accessors
        LinearCalibration CalibrationDataInternal { get; }
        LinearCalibration CalibrationDataADS1115 { get; }
        string GetCalibrationFilePath(bool adcMode);
        string GetTareFilePath();
        void ResetCalibration(bool adsMode);
        double TareValue { get; }
    }
}
