using ATS_TwoWheeler_WPF.Models;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IDataLoggerService
    {
        bool IsLogging { get; }
        
        bool StartLogging();
        void StopLogging();
        string GetLogFilePath();
        bool ExportToCSV(string exportPath);
        long GetLogFileSize();
        int GetLogLineCount();
        void UpdateSystemStatus(SystemStatus systemStatus, byte errorFlags, SystemMode relayState, ushort canTxHz, ushort adcSampleHz, uint uptime, string firmwareVersion);
        void LogDataPoint(string side, int rawADC, double calibratedKg, double taredKg, double tareBaseline, double calSlope, double calIntercept, AdcMode adcMode);
    }
}
