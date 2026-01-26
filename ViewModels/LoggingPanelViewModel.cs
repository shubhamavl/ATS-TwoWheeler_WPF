using System;
using System.Windows.Input;
using System.Windows.Media;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using Microsoft.Win32;
using System.Windows;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class LoggingPanelViewModel : BaseViewModel
    {
        private readonly IDataLoggerService _dataLogger;
        private readonly ICANService _canService; // For checking connection if needed, though StartLogging checks internal state

        private bool _isLogging;
        public bool IsLogging
        {
            get => _isLogging;
            set
            {
                if (SetProperty(ref _isLogging, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    ((RelayCommand)StartLoggingCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StopLoggingCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusText => _dataLogger.IsLogging 
            ? $"Logging to: {_dataLogger.GetLogFilePath()}" 
            : "Not logging";

        public Brush StatusColor => _dataLogger.IsLogging 
            ? new SolidColorBrush(Color.FromRgb(40, 167, 69)) // Green
            : new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

        private int _sampleCount;
        public int SampleCount
        {
            get => _sampleCount;
            set => SetProperty(ref _sampleCount, value);
        }

        public ICommand StartLoggingCommand { get; }
        public ICommand StopLoggingCommand { get; }
        public ICommand ExportLogCommand { get; }

        public LoggingPanelViewModel(IDataLoggerService dataLogger, ICANService canService)
        {
            _dataLogger = dataLogger;
            _canService = canService;

            StartLoggingCommand = new RelayCommand(OnStartLogging, _ => !IsLogging);
            StopLoggingCommand = new RelayCommand(OnStopLogging, _ => IsLogging);
            ExportLogCommand = new RelayCommand(OnExportLog);
            
            // Sync initial state
            IsLogging = _dataLogger.IsLogging;
            UpdateSampleCount();
            
            // Subscribe to timer or service events to update sample count?
            // DataLogger doesn't seem to have a "SampleCountChanged" event in interface I defined.
            // I might need to poll or adding an event to IDataLoggerService would be better.
            // For now, assume a timer in MainWindowViewModel might call a Refresh method here, 
            // or we add a polling timer here. 
            // Let's add a public Refresh method.
        }

        public void Refresh()
        {
            IsLogging = _dataLogger.IsLogging; // Update state if changed externally
            UpdateSampleCount();
        }

        private void UpdateSampleCount()
        {
            SampleCount = _dataLogger.GetLogLineCount();
        }

        private void OnStartLogging(object? parameter)
        {
            if (_dataLogger.StartLogging())
            {
                IsLogging = true;
                // TODO: Update Views/MainWindow logic to update LogsWindow if needed
            }
            else
            {
                MessageBox.Show("Failed to start data logging.", "Logging Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnStopLogging(object? parameter)
        {
            _dataLogger.StopLogging();
            IsLogging = false;
        }

        private void OnExportLog(object? parameter)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"two_wheeler_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };
                
                if (saveDialog.ShowDialog() == true)
                {
                    if (_dataLogger.ExportToCSV(saveDialog.FileName))
                    {
                        MessageBox.Show($"Data exported successfully to:\n{saveDialog.FileName}", 
                                      "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to export data.", "Export Error", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Export Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    // Extensions helper just for this simple command implementation update
    public static class CommandExtensions
    {
        public static void RaiseCanExecuteChanged(this ICommand command)
        {
            (command as RelayCommand)?.GetType().GetMethod("RaiseCanExecuteChanged")?.Invoke(command, null);
             // Note: My RelayCommand implementation uses CommandManager.RequerySuggested so manual raise isn't always needed 
             // but if I wanted strict manual control I'd implement a Raise method. 
             // With CommandManager.RequerySuggested, UI updates automatically on idle.
        }
    }
}
