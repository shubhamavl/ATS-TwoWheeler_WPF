using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.ViewModels.Base;

namespace ATS_TwoWheeler_WPF.ViewModels.Bootloader
{
    /// <summary>
    /// View models for bootloader diagnostics data binding
    /// </summary>
    public class BootloaderMessageViewModel
    {
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; } = "";
        public uint CanId { get; set; }
        public string Description { get; set; } = "";
        public string DataHex { get; set; } = "";
    }

    public class BootloaderErrorViewModel
    {
        public DateTime Timestamp { get; set; }
        public string ErrorCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string SuggestedResolution { get; set; } = "";
    }

    public class BootloaderOperation
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = "";
        public string Direction { get; set; } = "";
        public uint CanId { get; set; }
        public string Status { get; set; } = "";
        public string Details { get; set; } = "";
    }

    /// <summary>
    /// Manages bootloader diagnostics including messages, errors, and operation logs
    /// </summary>
    public class BootloaderDiagnosticsViewModel : BaseViewModel
    {
        private readonly BootloaderDiagnosticsService _diagnosticsService;
        private readonly System.Windows.Threading.DispatcherTimer _syncTimer;

        public ObservableCollection<BootloaderMessageViewModel> Messages { get; } = new();
        public ObservableCollection<BootloaderErrorViewModel> Errors { get; } = new();
        public ObservableCollection<BootloaderOperation> OperationLog { get; } = new();

        public BootloaderDiagnosticsViewModel(BootloaderDiagnosticsService diagnosticsService)
        {
            _diagnosticsService = diagnosticsService;

            // Subscribe to diagnostics service changes
            _diagnosticsService.Messages.CollectionChanged += (s, e) => SyncMessages();

            // Start timer for periodic error synchronization
            _syncTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _syncTimer.Tick += (s, e) => SyncErrors();
            _syncTimer.Start();
        }

        /// <summary>
        /// Synchronize messages from diagnostics service
        /// </summary>
        private void SyncMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Clear();
                foreach (var msg in _diagnosticsService.Messages)
                {
                    Messages.Add(new BootloaderMessageViewModel
                    {
                        Timestamp = msg.Timestamp,
                        Direction = msg.Direction,
                        CanId = msg.CanId,
                        Description = msg.Description,
                        DataHex = BitConverter.ToString(msg.Data).Replace("-", " ")
                    });
                }
            });
        }

        /// <summary>
        /// Synchronize errors from diagnostics service
        /// </summary>
        private void SyncErrors()
        {
            var currentErrors = _diagnosticsService.Errors.ToList();
            if (currentErrors.Count != Errors.Count)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Errors.Clear();
                    foreach (var err in currentErrors)
                    {
                        Errors.Add(new BootloaderErrorViewModel
                        {
                            Timestamp = err.Timestamp,
                            ErrorCode = err.ErrorCode.ToString(),
                            Description = err.Description,
                            SuggestedResolution = err.SuggestedResolution
                        });
                    }
                });
            }
        }

        /// <summary>
        /// Log an operation to the operation log
        /// </summary>
        public void LogOperation(string operation, string direction, uint canId, string status, string details)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OperationLog.Add(new BootloaderOperation
                {
                    Timestamp = DateTime.Now,
                    Operation = operation,
                    Direction = direction,
                    CanId = canId,
                    Status = status,
                    Details = details
                });
            });
        }

        /// <summary>
        /// Clear all messages
        /// </summary>
        public void ClearMessages()
        {
            _diagnosticsService.ClearMessages();
            Messages.Clear();
        }

        /// <summary>
        /// Clear all errors
        /// </summary>
        public void ClearErrors()
        {
            _diagnosticsService.ClearErrors();
            Errors.Clear();
        }

        /// <summary>
        /// Clear operation log
        /// </summary>
        public void ClearOperationLog()
        {
            OperationLog.Clear();
        }

        /// <summary>
        /// Export messages to text format
        /// </summary>
        public string ExportMessagesToText()
        {
            return _diagnosticsService.ExportMessagesToText();
        }

        /// <summary>
        /// Export operation log to text format
        /// </summary>
        public string ExportOperationLogToText()
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "Bootloader Operation Log",
                "=======================",
                $"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                ""
            };

            foreach (var op in OperationLog)
            {
                lines.Add($"[{op.Timestamp:HH:mm:ss.fff}] {op.Direction} {op.Operation} (0x{op.CanId:X3}) - {op.Status}: {op.Details}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public override void Dispose()
        {
            _syncTimer?.Stop();
            base.Dispose();
        }
    }
}
