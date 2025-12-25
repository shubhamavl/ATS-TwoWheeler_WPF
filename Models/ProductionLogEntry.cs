using System;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Production logger log entry
    /// </summary>
    public class ProductionLogEntry
    {
        public DateTime Timestamp { get; set; }
        public ProductionLogger.LogLevel Level { get; set; }
        public string Message { get; set; } = "";
        public string Source { get; set; } = "";

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] {Level}: {Message}";
        public string LevelText => Level.ToString();
    }
}

