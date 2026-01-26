using System;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services
{
    /// <summary>
    /// Adapter that wraps ProductionLogger singleton to implement ILogger interface
    /// Enables dependency injection and testability
    /// </summary>
    public class ProductionLoggerAdapter : ILogger
    {
        private readonly ProductionLogger _logger;

        public ProductionLoggerAdapter()
        {
            _logger = ProductionLogger.Instance;
        }

        public void LogDebug(string message, string source = "")
        {
            // ProductionLogger doesn't have Debug level, use Info
            _logger.LogInfo(message, source);
        }

        public void LogInfo(string message, string source = "")
        {
            _logger.LogInfo(message, source);
        }

        public void LogWarning(string message, string source = "")
        {
            _logger.LogWarning(message, source);
        }

        public void LogError(string message, string source = "")
        {
            _logger.LogError(message, source);
        }

        public void LogError(Exception ex, string message, string source = "")
        {
            _logger.LogError($"{message}: {ex.Message}", source);
        }
    }
}
