using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IProductionLoggerService : INotifyPropertyChanged
    {
        ObservableCollection<ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry> LogEntries { get; }
        bool IsEnabled { get; set; }
        ATS_TwoWheeler_WPF.Services.ProductionLogger.LogLevel MinimumLevel { get; set; }

        void Log(ATS_TwoWheeler_WPF.Services.ProductionLogger.LogLevel level, string message, string source = "");
        void LogInfo(string message, string source = "");
        void LogWarning(string message, string source = "");
        void LogError(string message, string source = "");
        void LogCritical(string message, string source = "");
        void ClearLogs();
        bool ExportLogs(string filePath);
        IEnumerable<ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry> GetFilteredLogs(bool showInfo, bool showWarning, bool showError, bool showCritical);
        string GetLogFilePath();
        int GetLogCount();
        IEnumerable<ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry> GetAllLogsSnapshot();
    }
}
