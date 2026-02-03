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



    /// <summary>
    /// Manages bootloader diagnostics including messages, errors, and operation logs
    /// </summary>
    public class BootloaderDiagnosticsViewModel : BaseViewModel
    {
        private readonly BootloaderDiagnosticsService _diagnosticsService;

        public ObservableCollection<BootloaderMessageViewModel> Messages { get; } = new();
        public ObservableCollection<BootloaderErrorViewModel> Errors { get; } = new();
        public ObservableCollection<BootloaderOperation> OperationLog { get; } = new();

        public BootloaderDiagnosticsViewModel(BootloaderDiagnosticsService diagnosticsService)
        {
            _diagnosticsService = diagnosticsService;

            // Subscribe to new message event (thread-safe)
            _diagnosticsService.MessageCaptured += OnMessageCaptured;
            _diagnosticsService.OperationLogged += OnOperationLogged;
            _diagnosticsService.ErrorRecorded += OnErrorRecorded;

            // Load initial data
            LoadInitialMessages();
            LoadInitialOperations();
            LoadInitialErrors();
        }

        private void OnOperationLogged(object? sender, BootloaderOperation op)
        {
            Application.Current.Dispatcher.Invoke(() => OperationLog.Add(op));
        }

        private void OnErrorRecorded(object? sender, BootloaderError error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Errors.Add(new BootloaderErrorViewModel
                {
                    Timestamp = error.Timestamp,
                    ErrorCode = error.ErrorCode.ToString(),
                    Description = error.Description,
                    SuggestedResolution = error.SuggestedResolution
                });
            });
        }

        private void LoadInitialOperations()
        {
            var initialOps = _diagnosticsService.OperationLog;
            foreach (var op in initialOps)
            {
                OperationLog.Add(op);
            }
        }

        private void LoadInitialErrors()
        {
            var initialErrors = _diagnosticsService.Errors;
            foreach (var error in initialErrors)
            {
                Errors.Add(new BootloaderErrorViewModel
                {
                    Timestamp = error.Timestamp,
                    ErrorCode = error.ErrorCode.ToString(),
                    Description = error.Description,
                    SuggestedResolution = error.SuggestedResolution
                });
            }
        }


        /// <summary>
        /// Handles new messages captured by the diagnostics service.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMessageCaptured(object? sender, BootloaderMessage message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new BootloaderMessageViewModel
                {
                    Timestamp = message.Timestamp,
                    Direction = message.IsTx ? "TX" : "RX",
                    CanId = message.CanId,
                    Description = message.Description,
                    DataHex = BitConverter.ToString(message.Data).Replace("-", " ")
                });
            });
        }

        /// <summary>
        /// Loads initial messages from the diagnostics service.
        /// </summary>
        private void LoadInitialMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var msg in _diagnosticsService.GetMessages())
                {
                    Messages.Add(new BootloaderMessageViewModel
                    {
                        Timestamp = msg.Timestamp,
                        Direction = msg.IsTx ? "TX" : "RX",
                        CanId = msg.CanId,
                        Description = msg.Description,
                        DataHex = BitConverter.ToString(msg.Data).Replace("-", " ")
                    });
                }
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
            _diagnosticsService.ClearOperationLog();
            OperationLog.Clear();
        }

        /// <summary>
        /// Log an operation to the operation log
        /// </summary>
        public void LogOperation(string operation, string direction, uint canId, string status, string details)
        {
            _diagnosticsService.LogOperation(operation, direction, canId, status, details);
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
            _diagnosticsService.MessageCaptured -= OnMessageCaptured;
            _diagnosticsService.OperationLogged -= OnOperationLogged;
            _diagnosticsService.ErrorRecorded -= OnErrorRecorded;
            base.Dispose();
        }
    }
}
