using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class LogsViewModel : BaseViewModel
    {
        private readonly IProductionLoggerService _logger;
        private readonly IDataLoggerService _dataLogger;
        private readonly IDialogService _dialogService;
        
        // Batching for performance
        private readonly System.Collections.Concurrent.ConcurrentQueue<ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry> _logPendingQueue = new();
        private readonly DispatcherTimer _batchTimer;

        private readonly ObservableCollection<LogEntry> _allLogEntries = new();
        public ReadOnlyObservableCollection<LogEntry> AllLogEntries { get; }

        private readonly ObservableCollection<LogEntry> _filteredLogEntries = new();
        public ReadOnlyObservableCollection<LogEntry> FilteredLogEntries { get; }

        private bool _showInfo = true;
        public bool ShowInfo
        {
            get => _showInfo;
            set { if (SetProperty(ref _showInfo, value)) ApplyFilters(); }
        }

        private bool _showWarning = true;
        public bool ShowWarning
        {
            get => _showWarning;
            set { if (SetProperty(ref _showWarning, value)) ApplyFilters(); }
        }

        private bool _showError = true;
        public bool ShowError
        {
            get => _showError;
            set { if (SetProperty(ref _showError, value)) ApplyFilters(); }
        }

        private bool _showCritical = true;
        public bool ShowCritical
        {
            get => _showCritical;
            set { if (SetProperty(ref _showCritical, value)) ApplyFilters(); }
        }

        public bool IsLogging => _dataLogger.IsLogging;

        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }

        public LogsViewModel(IProductionLoggerService logger, IDataLoggerService dataLogger, IDialogService dialogService)
        {
            _logger = logger;
            _dataLogger = dataLogger;
            _dialogService = dialogService;

            AllLogEntries = new ReadOnlyObservableCollection<LogEntry>(_allLogEntries);
            FilteredLogEntries = new ReadOnlyObservableCollection<LogEntry>(_filteredLogEntries);

            ClearLogsCommand = new RelayCommand(_ => ClearLogs());
            ExportLogsCommand = new RelayCommand(async _ => await ExportLogsAsync());
            OpenLogsFolderCommand = new RelayCommand(_ => OnOpenLogsFolder());

            // Initialize from existing logs (using thread-safe snapshot)
            foreach (var entry in _logger.GetAllLogsSnapshot())
            {
                AddLogEntry(entry);
            }

            // Initialize from existing logs (using thread-safe snapshot)
            foreach (var entry in _logger.GetAllLogsSnapshot())
            {
                AddLogEntry(entry);
            }

            _logger.LogEntries.CollectionChanged += OnLoggerCollectionChanged;

            // Setup batch timer (updates UI every 250ms)
            _batchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _batchTimer.Tick += OnBatchTimerTick;
            _batchTimer.Start();
        }

        private void OnLoggerCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry entry in e.NewItems)
                {
                    _logPendingQueue.Enqueue(entry);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // For reset, do it immediately on UI thread
                 System.Windows.Application.Current.Dispatcher.Invoke(() =>
                 {
                    _allLogEntries.Clear();
                    _filteredLogEntries.Clear();
                 });
            }
        }

        private void OnBatchTimerTick(object? sender, EventArgs e)
        {
            if (_logPendingQueue.IsEmpty) return;

            var batch = new List<ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry>();
            while (_logPendingQueue.TryDequeue(out var entry))
            {
                batch.Add(entry);
                // Limit batch size to prevent freezing if queue is huge
                if (batch.Count > 500) break; 
            }

            if (batch.Count > 0)
            {
                 // AddLogEntry logic inline or reused (AddLogEntry adds to collections)
                foreach (var entry in batch)
                {
                    AddLogEntry(entry);
                }
            }
        }

        private void AddLogEntry(ATS_TwoWheeler_WPF.Services.ProductionLogger.LogEntry entry)
        {
            var logEntry = new LogEntry
            {
                Timestamp = entry.Timestamp,
                Message = entry.Message,
                Level = entry.Level.ToString(),
                Source = string.IsNullOrEmpty(entry.Source) ? "ProductionLogger" : entry.Source
            };

            _allLogEntries.Add(logEntry);
            if (MatchesFilter(logEntry))
            {
                _filteredLogEntries.Add(logEntry);
            }
        }

        private void ApplyFilters()
        {
            _filteredLogEntries.Clear();
            foreach (var entry in _allLogEntries)
            {
                if (MatchesFilter(entry))
                {
                    _filteredLogEntries.Add(entry);
                }
            }
        }

        private bool MatchesFilter(LogEntry entry)
        {
            string level = entry.Level.ToUpper();
            if (level.Contains("INFO") && !ShowInfo) return false;
            if (level.Contains("WARNING") && !ShowWarning) return false;
            if (level.Contains("ERROR") && !ShowError) return false;
            if (level.Contains("CRITICAL") && !ShowCritical) return false;
            return true;
        }

        private void ClearLogs()
        {
            if (_dialogService.ShowConfirmation("Clear all log entries from display?\n\nNote: This only clears the display. Log files are not affected.", "Confirm Clear"))
            {
                _logger.ClearLogs();
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                string? filePath = _dialogService.ShowSaveFileDialog("CSV Files (*.csv)|*.csv|All Files (*.*)|*.*", $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv", "Export Logs");
                
                if (filePath == null) return;

                await Task.Run(() =>
                {
                    using (var writer = new System.IO.StreamWriter(filePath))
                    {
                        writer.WriteLine("Timestamp,Level,Message,Source");
                        
                        // Snapshot for thread safety
                        var logs = new List<LogEntry>(_filteredLogEntries);

                        foreach (var entry in logs)
                        {
                            writer.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{entry.Level},\"{entry.Message}\",{entry.Source}");
                        }
                    }
                });
                
                _dialogService.ShowMessage($"Logs exported to: {filePath}", "Export Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Export error: {ex.Message}", "Error");
            }
        }

        private void OnOpenLogsFolder()
        {
            try
            {
                string logsDir = PathHelper.GetLogsDirectory();
                if (System.IO.Directory.Exists(logsDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logsDir);
                }
                else
                {
                    _dialogService.ShowWarning("Logs directory does not exist yet.", "Directory Not Found");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error opening logs folder: {ex.Message}", "Error");
            }
        }

        public void Cleanup()
        {
            _batchTimer.Stop();
            _logger.LogEntries.CollectionChanged -= OnLoggerCollectionChanged;
        }
    }
}
