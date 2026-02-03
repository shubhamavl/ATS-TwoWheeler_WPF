using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.ViewModels.Bootloader;
using ATS_TwoWheeler_WPF.Core.Exceptions;
using System.ComponentModel;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    /// <summary>
    /// Main ViewModel for bootloader management UI
    /// Coordinates firmware updates, bootloader operations, and diagnostics display
    /// </summary>
    public class BootloaderViewModel : BaseViewModel
    {
        private readonly CANService _canService;
        private readonly CANBootloaderService _bootloaderService;
        private readonly FirmwareUpdateService _firmwareUpdateService;
        private readonly BootloaderDiagnosticsService _diagnosticsService;
        private readonly IDialogService _dialogService;
        private readonly ProductionLogger _logger = ProductionLogger.Instance;

        private readonly BootloaderStateMachine _stateMachine;
        private readonly BootloaderDiagnosticsViewModel _diagnostics;
        private readonly BootloaderEventHandlers _eventHandlers;

        private CancellationTokenSource? _updateCts;
        private DateTime _updateStartTime;
        private long _bytesSent;
        private long _totalBytes;
        private int _totalChunks;
        private string? _selectedFirmwarePath;

        #region Firmware File Selection

        private string _firmwarePath = "No file selected";
        public string FirmwarePath
        {
            get => _firmwarePath;
            set
            {
                if (SetProperty(ref _firmwarePath, value))
                {
                    OnPropertyChanged(nameof(CanStartUpdate));
                }
            }
        }

        private string _firmwareSize = "";
        public string FirmwareSize
        {
            get => _firmwareSize;
            set => SetProperty(ref _firmwareSize, value);
        }

        #endregion

        #region Bootloader Info

        private BootloaderInfo _bootloaderInfo = new();
        public BootloaderInfo BootloaderInfo
        {
            get => _bootloaderInfo;
            set => SetProperty(ref _bootloaderInfo, value);
        }

        private string _bootloaderStatusText = "Bootloader: Unknown";
        public string BootloaderStatusText
        {
            get => _bootloaderStatusText;
            set => SetProperty(ref _bootloaderStatusText, value);
        }

        private string _firmwareVersionText = "Firmware: Unknown";
        public string FirmwareVersionText
        {
            get => _firmwareVersionText;
            set => SetProperty(ref _firmwareVersionText, value);
        }

        private string _lastUpdateText = "Last Update: Never";
        public string LastUpdateText
        {
            get => _lastUpdateText;
            set => SetProperty(ref _lastUpdateText, value);
        }

        #endregion

        #region Update Progress

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _progressLabel = "0%";
        public string ProgressLabel
        {
            get => _progressLabel;
            set => SetProperty(ref _progressLabel, value);
        }

        private string _transferRate = "";
        public string TransferRate
        {
            get => _transferRate;
            set => SetProperty(ref _transferRate, value);
        }

        private string _bytesTransferred = "0 / 0";
        public string BytesTransferred
        {
            get => _bytesTransferred;
            set => SetProperty(ref _bytesTransferred, value);
        }

        private string _timeElapsed = "00:00";
        public string TimeElapsed
        {
            get => _timeElapsed;
            set => SetProperty(ref _timeElapsed, value);
        }

        private string _timeRemaining = "--:--";
        public string TimeRemaining
        {
            get => _timeRemaining;
            set => SetProperty(ref _timeRemaining, value);
        }

        private string _statusText = "Status: Idle";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        #endregion

        #region Update State

        private bool _isUpdating;
        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (SetProperty(ref _isUpdating, value))
                {
                    OnPropertyChanged(nameof(CanStartUpdate));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public bool CanStartUpdate => !IsUpdating && File.Exists(_selectedFirmwarePath);
        public bool CanCancel => IsUpdating;

        #endregion

        #region Process Step (delegated to State Machine)

        public BootloaderProcessStep CurrentStep
        {
            get => _stateMachine.CurrentStep;
            set
            {
                if (_stateMachine.CurrentStep != value)
                {
                    _stateMachine.ForceSet(value);
                }
            }
        }

        #endregion

        #region Diagnostics (delegated to Diagnostics ViewModel)

        public ObservableCollection<BootloaderMessageViewModel> Messages => _diagnostics.Messages;
        public ObservableCollection<BootloaderErrorViewModel> Errors => _diagnostics.Errors;
        public ObservableCollection<BootloaderOperation> OperationLog => _diagnostics.OperationLog;

        #endregion

        #region Commands

        public ICommand BrowseCommand { get; }
        public ICommand StartUpdateCommand { get; }
        public ICommand CancelUpdateCommand { get; }
        public ICommand TestBootloaderCommand { get; }
        public ICommand EnterBootloaderCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand QueryInfoCommand { get; }
        public ICommand ClearMessagesCommand { get; }
        public ICommand ClearErrorsCommand { get; }
        public ICommand ExportMessagesCommand { get; }
        public ICommand ClearOperationLogCommand { get; }
        public ICommand ExportOperationLogCommand { get; }

        #endregion

        public BootloaderViewModel(
            ICANService canService,
            FirmwareUpdateService firmwareUpdateService,
            BootloaderDiagnosticsService diagnosticsService,
            IDialogService dialogService)
        {
            _canService = (CANService)canService;
            _bootloaderService = new CANBootloaderService(canService);
            _firmwareUpdateService = firmwareUpdateService;
            _dialogService = dialogService;

            _diagnosticsService = diagnosticsService;

            // Initialize state machine and diagnostics
            _stateMachine = new BootloaderStateMachine();
            _stateMachine.StepChanged += OnStepChanged;

            _diagnostics = new BootloaderDiagnosticsViewModel(diagnosticsService);

            // Set diagnostics service in firmware update service
            _firmwareUpdateService.SetDiagnosticsService(diagnosticsService);

            // Initialize commands
            BrowseCommand = new RelayCommand(_ => OnBrowse());
            StartUpdateCommand = new AsyncRelayCommand(OnStartUpdate, () => CanStartUpdate);
            CancelUpdateCommand = new RelayCommand(_ => OnCancel(), _ => CanCancel);
            TestBootloaderCommand = new AsyncRelayCommand(OnTestBootloader);
            EnterBootloaderCommand = new AsyncRelayCommand(OnEnterBootloader);
            ResetCommand = new RelayCommand(_ => OnReset());
            QueryInfoCommand = new AsyncRelayCommand(OnQueryInfo);
            ClearMessagesCommand = new RelayCommand(_ => _diagnostics.ClearMessages());
            ClearErrorsCommand = new RelayCommand(_ => _diagnostics.ClearErrors());
            ExportMessagesCommand = new RelayCommand(_ => OnExportMessages());
            ClearOperationLogCommand = new RelayCommand(_ => _diagnostics.ClearOperationLog());
            ExportOperationLogCommand = new RelayCommand(_ => OnExportOperationLog());

            // Subscribe to CAN service events
            _canService.MessageReceived += OnCANMessageReceived;

            // Initialize event handlers (handles bootloader service subscriptions)
            _eventHandlers = new BootloaderEventHandlers(this, _bootloaderService, _diagnostics, _stateMachine);

            UpdateUI();
        }

        private void OnStepChanged(object? sender, BootloaderProcessStep newStep)
        {
            OnPropertyChanged(nameof(CurrentStep));
            StatusText = $"Status: {_stateMachine.GetStepDescription()}";
        }

        private void OnCANMessageReceived(CANMessage msg)
        {
            // Capture all messages in diagnostics service
            // The service filters for bootloader-relevant IDs (0x510-0x51F)
            bool isTx = msg.Direction == "TX";
            _diagnosticsService.CaptureMessage(msg.ID, msg.Data, isTx);
        }

        internal void UpdateUI()
        {
            BootloaderStatusText = BootloaderInfo.IsPresent
                ? $"Bootloader: {BootloaderInfo.StatusDescription}"
                : "Bootloader: Not Responding";

            FirmwareVersionText = BootloaderInfo.FirmwareVersion != null
                ? $"Firmware: {BootloaderInfo.FirmwareVersion}"
                : "Firmware: Unknown";

            LastUpdateText = BootloaderInfo.LastUpdateTime.HasValue
                ? $"Last Update: {BootloaderInfo.LastUpdateTime.Value:yyyy-MM-dd HH:mm:ss}"
                : "Last Update: Never";
        }

        #region Command Implementations

        private void OnBrowse()
        {
            try
            {
                var firmwarePath = _dialogService.ShowOpenFileDialog("Firmware Binary (*.bin)|*.bin|All files (*.*)|*.*");
                if (!string.IsNullOrEmpty(firmwarePath))
                {
                    FirmwarePath = Path.GetFileName(firmwarePath);
                    var fileInfo = new FileInfo(firmwarePath);
                    long appSize = Math.Max(0, fileInfo.Length - 0x2000); // Subtract bootloader size
                    FirmwareSize = $"{appSize:N0} bytes";

                    // Store full path internally for update
                    _selectedFirmwarePath = firmwarePath;
                }
            }

            catch (Win32Exception ex)
            {
                _logger.LogError($"Firmware browse dialog error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Dialog error: {ex.Message}", "Error");
            }
            catch (IOException ex)
            {
                _logger.LogError($"Firmware file access error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"File error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected firmware browse error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Failed to select firmware: {ex.Message}", "Error");
            }
        }

        private async Task OnStartUpdate()
        {
            if (!ValidateFirmwareSelection())
            {
                return;
            }

            if (!ConfirmFirmwareUpdate())
            {
                return;
            }

            InitializeUpdateState();

            var progress = CreateProgressHandler();

            try
            {
                bool success = await _firmwareUpdateService.UpdateFirmwareAsync(
                    _selectedFirmwarePath!, progress, _updateCts!.Token);

                if (success)
                {
                    HandleUpdateSuccess();
                }
                else
                {
                    HandleUpdateFailure("Firmware update failed - check diagnostics");
                }
            }
            catch (OperationCanceledException)
            {
                HandleUpdateCancelled();
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError($"Firmware file not found: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"File not found: {ex.Message}", "Error");
            }
            catch (IOException ex)
            {
                _logger.LogError($"Firmware IO error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Disk error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Firmware update error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Firmware update error: {ex.Message}", "Error");
            }
            finally
            {
                CleanupUpdateState();
            }
        }

        private bool ValidateFirmwareSelection()
        {
            if (string.IsNullOrEmpty(_selectedFirmwarePath) || !File.Exists(_selectedFirmwarePath))
            {
                _dialogService.ShowError("Please select a firmware file first.", "Error");
                return false;
            }
            return true;
        }

        private bool ConfirmFirmwareUpdate()
        {
            return _dialogService.ShowConfirmation(
                $"Update firmware?\n\n" +
                $"File: {Path.GetFileName(_selectedFirmwarePath)}\n" +
                $"This will erase the application and write new firmware.",
                "Confirm Firmware Update");
        }

        private void InitializeUpdateState()
        {
            IsUpdating = true;
            _updateCts = new CancellationTokenSource();
            _updateStartTime = DateTime.Now;
            _bytesSent = 0;

            var fileInfo = new FileInfo(_selectedFirmwarePath!);
            _totalBytes = Math.Max(0, fileInfo.Length - 0x2000);
            _totalChunks = (int)((_totalBytes + 6) / 7);

            ProgressValue = 0;
            ProgressLabel = "0%";
            BytesTransferred = $"0 / {_totalBytes:N0}";
            TimeElapsed = "00:00";
            TimeRemaining = "--:--";
            StatusText = "Status: Entering bootloader mode...";

            _diagnostics.LogOperation("Start Update", "TX", 0, "In Progress",
                $"File: {Path.GetFileName(_selectedFirmwarePath)}, Size: {_totalBytes:N0} bytes");
        }

        private IProgress<FirmwareProgress> CreateProgressHandler()
        {
            return new Progress<FirmwareProgress>(p =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressValue = p.Percentage;
                    ProgressLabel = $"{p.Percentage:0}% ({p.ChunksSent}/{p.TotalChunks})";

                    var elapsed = DateTime.Now - _updateStartTime;
                    if (elapsed.TotalSeconds > 0)
                    {
                        _bytesSent = (long)(_totalBytes * p.Percentage / 100.0);
                        double bytesPerSecond = _bytesSent / elapsed.TotalSeconds;
                        TransferRate = bytesPerSecond > 1024
                            ? $"{bytesPerSecond / 1024:F1} KB/s"
                            : $"{bytesPerSecond:F0} B/s";

                        UpdateDetailedProgress(_bytesSent, (int)p.Percentage);
                    }
                });
            });
        }

        private void HandleUpdateSuccess()
        {
            CurrentStep = BootloaderProcessStep.Complete;
            _diagnostics.LogOperation("Update Complete", "SYSTEM", 0, "Success",
                "Firmware update completed successfully - STM32 resetting");
            StatusText = "Status: Update complete! System will reset.";

            _dialogService.ShowMessage(
                "Firmware update completed successfully!\n\nThe system will reset and boot from the new firmware.",
                "Update Complete");
        }

        private void HandleUpdateFailure(string message)
        {
            CurrentStep = BootloaderProcessStep.Failed;
            _diagnostics.LogOperation("Update Failed", "SYSTEM", 0, "Failed", message);
            StatusText = $"Status: {message}";

            _dialogService.ShowError(
                "Firmware update failed.\n\nCheck the diagnostics tab for error details.",
                "Update Failed");
        }

        private void HandleUpdateCancelled()
        {
            CurrentStep = BootloaderProcessStep.Failed;
            _diagnostics.LogOperation("Update Cancelled", "SYSTEM", 0, "Cancelled",
                "Firmware update was cancelled by user");
            StatusText = "Status: Update cancelled";

            _dialogService.ShowMessage("Firmware update was cancelled.", "Update Cancelled");
        }

        private void CleanupUpdateState()
        {
            IsUpdating = false;
            _stateMachine.Reset();
            _updateCts?.Dispose();
            _updateCts = null;
            StatusText = "Status: Idle";
        }

        internal void UpdateDetailedProgress(long bytes, int percent)
        {
            BytesTransferred = $"{bytes:N0} / {_totalBytes:N0}";
            var elapsed = DateTime.Now - _updateStartTime;
            TimeElapsed = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";

            if (percent > 0 && elapsed.TotalSeconds > 0)
            {
                double remainingSeconds = elapsed.TotalSeconds / percent * (100 - percent);
                var remaining = TimeSpan.FromSeconds(remainingSeconds);
                TimeRemaining = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            }
        }

        private void OnCancel()
        {
            _updateCts?.Cancel();
        }

        private async Task OnTestBootloader()
        {
            try
            {
                bool sent = _canService.SendMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>());

                if (sent)
                {
                    CurrentStep = BootloaderProcessStep.Ping;
                    _diagnostics.LogOperation("Ping", "TX", BootloaderProtocol.CanIdBootPing, "Sent",
                        "Testing bootloader communication");
                    StatusText = "Status: Pinging bootloader...";

                    await Task.Delay(2000);

                    if (BootloaderInfo.Status == BootloaderStatus.Ready)
                    {
                        _dialogService.ShowMessage("Bootloader is responding!\n\nStatus: READY", "Test Successful");
                    }
                    else
                    {
                        _dialogService.ShowError(
                            "No response from bootloader.\n\nPossible causes:\n• STM32 is not in bootloader mode\n• CAN bus communication issue\n• Bootloader not present",
                            "Test Failed");
                    }
                }
                else
                {
                    _dialogService.ShowError("Failed to send ping command.", "Error");
                }
            }
            catch (CANSendException ex)
            {
                _logger.LogError($"Test bootloader CAN error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Communication Error: {ex.Message}", "Error");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Test bootloader timeout: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError("Communication timed out.", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Test bootloader error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private async Task OnEnterBootloader()
        {
            var result = _dialogService.ShowConfirmation(
                "Enter Bootloader Mode?\n\n" +
                "This will cause the STM32 to:\n" +
                "1. Set entry magic in RTC backup register\n" +
                "2. Reset immediately\n" +
                "3. Boot into bootloader mode\n\n" +
                "The system will be ready for firmware updates.",
                "Confirm Enter Bootloader");

            if (!result)
            {
                return;
            }

            try
            {
                bool sent = _bootloaderService.RequestEnterBootloader();

                if (sent)
                {
                    CurrentStep = BootloaderProcessStep.EnterBootloader;
                    _diagnostics.LogOperation("Enter Bootloader", "TX", BootloaderProtocol.CanIdBootEnter, "Sent",
                        "Requesting bootloader entry");
                    StatusText = "Status: Entering bootloader mode... Waiting for reset...";

                    // Auto-ping logic (matching the philosophy of immediately requesting status after state change)
                    await Task.Delay(3500); // Wait for STM32 reset cycle
                    await OnTestBootloader();
                }
                else
                {
                    _dialogService.ShowError("Failed to send Enter Bootloader command.", "Error");
                }
            }
            catch (CANSendException ex)
            {
                _logger.LogError($"Enter bootloader CAN error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Communication Error: {ex.Message}", "Error");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Enter bootloader timeout: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError("Communication timed out.", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Enter bootloader error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private void OnReset()
        {
            var result = _dialogService.ShowConfirmation(
                "Reset STM32?\n\n" +
                "This will cause the STM32 to reset immediately.\n" +
                "The system will boot from the active bank.",
                "Confirm Reset");

            if (!result)
            {
                return;
            }

            try
            {
                bool sent = _bootloaderService.RequestReset();
                if (sent)
                {
                    _dialogService.ShowMessage("Reset command sent. STM32 will reset shortly.", "Reset Sent");
                }
                else
                {
                    _dialogService.ShowError("Failed to send reset command.", "Error");
                }
            }
            catch (CANSendException ex)
            {
                _logger.LogError($"Reset CAN error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Communication Error: {ex.Message}", "Error");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Reset timeout: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError("Communication timed out.", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reset error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private async Task OnQueryInfo()
        {
            try
            {
                bool sent = _bootloaderService.QueryBootloaderInfo();
                if (sent)
                {
                    _diagnostics.LogOperation("Query Boot Info", "TX", BootloaderProtocol.CanIdBootQueryInfo, "Sent",
                        "Requesting bootloader information");
                    await Task.Delay(2000);

                    if (BootloaderInfo.IsPresent)
                    {
                        _dialogService.ShowMessage(
                            $"Bootloader Info:\n\n" +
                            $"Present: Yes\n" +
                            $"Version: {BootloaderInfo.FirmwareVersion?.ToString() ?? "Unknown"}\n" +
                            $"Status: {BootloaderInfo.StatusDescription}",
                            "Bootloader Info");
                    }
                    else
                    {
                        _dialogService.ShowError(
                            "No response from bootloader. Ensure STM32 is in bootloader mode or application mode.",
                            "No Response");
                    }
                }
                else
                {
                    _dialogService.ShowError("Failed to request bootloader info.", "Error");
                }
            }
            catch (CANSendException ex)
            {
                _logger.LogError($"Query boot info CAN error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Communication Error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Query boot info error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private void OnExportMessages()
        {
            try
            {
                var fileName = $"bootloader_messages_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var path = _dialogService.ShowSaveFileDialog("Text Files (*.txt)|*.txt|All Files (*.*)|*.*", fileName);

                if (!string.IsNullOrEmpty(path))
                {
                    string content = _diagnostics.ExportMessagesToText();
                    File.WriteAllText(path, content);
                    _dialogService.ShowMessage($"Messages exported to:\n{path}", "Export Complete");
                }
            }
            catch (IOException ex)
            {
                _logger.LogError($"Export messages IO error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"File Error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export messages error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Failed to export messages: {ex.Message}", "Error");
            }
        }

        private void OnExportOperationLog()
        {
            try
            {
                var fileName = $"operation_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var path = _dialogService.ShowSaveFileDialog("Text Files (*.txt)|*.txt|All Files (*.*)|*.*", fileName);

                if (!string.IsNullOrEmpty(path))
                {
                    string content = _diagnostics.ExportOperationLogToText();
                    File.WriteAllText(path, content);
                    _dialogService.ShowMessage($"Operation log exported to:\n{path}", "Export Complete");
                }
            }
            catch (IOException ex)
            {
                _logger.LogError($"Export operation log IO error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"File Error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export operation log error: {ex.Message}", "BootloaderManager");
                _dialogService.ShowError($"Failed to export operation log: {ex.Message}", "Error");
            }
        }

        #endregion

        public override void Dispose()
        {
            // Unsubscribe from events
            _canService.MessageReceived -= OnCANMessageReceived;
            _eventHandlers.Dispose();
            _bootloaderService.Dispose();
            _diagnostics.Dispose();

            _updateCts?.Dispose();
            base.Dispose();
        }
    }
}
