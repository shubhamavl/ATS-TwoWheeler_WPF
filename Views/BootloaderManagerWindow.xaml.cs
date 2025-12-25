using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.Views
{
    public enum BootloaderProcessStep
    {
        Idle = 0,
        EnterBootloader = 1,
        Ping = 2,
        Begin = 3,
        Transfer = 4,
        End = 5,
        Reset = 6,
        Complete = 7,
        Failed = 8
    }

    public partial class BootloaderManagerWindow : Window
    {
        private readonly CANService? _canService;
        private readonly FirmwareUpdateService? _firmwareUpdateService;
        private readonly BootloaderDiagnosticsService? _diagnosticsService;
        private readonly ProductionLogger _logger = ProductionLogger.Instance;
        
        private string? _selectedFirmwarePath;
        private bool _updateInProgress;
        private CancellationTokenSource? _updateCts;
        private DateTime _updateStartTime;
        private long _bytesSent;
        
        private readonly ObservableCollection<BootloaderMessageViewModel> _messages = new ObservableCollection<BootloaderMessageViewModel>();
        private readonly ObservableCollection<BootloaderErrorViewModel> _errors = new ObservableCollection<BootloaderErrorViewModel>();
        private readonly ObservableCollection<BootloaderOperation> _operationLog = new ObservableCollection<BootloaderOperation>();
        
        private BootloaderInfo _bootloaderInfo = new BootloaderInfo();
        private BootloaderProcessStep _currentStep = BootloaderProcessStep.Idle;
        private long _totalBytes = 0;
        private int _totalChunks = 0;
        private byte _currentSequenceNumber = 0;

        public BootloaderManagerWindow(CANService? canService, FirmwareUpdateService? firmwareUpdateService, 
                                      BootloaderDiagnosticsService? diagnosticsService)
        {
            InitializeComponent();
            
            _canService = canService;
            _firmwareUpdateService = firmwareUpdateService;
            _diagnosticsService = diagnosticsService;
            
            // Set diagnostics service in firmware update service if available
            if (_firmwareUpdateService != null && _diagnosticsService != null)
            {
                _firmwareUpdateService.SetDiagnosticsService(_diagnosticsService);
            }
            
            // Set up data bindings
            MessagesDataGrid.ItemsSource = _messages;
            ErrorsDataGrid.ItemsSource = _errors;
            
            // Subscribe to diagnostics service events
            if (_diagnosticsService != null)
            {
                _diagnosticsService.Messages.CollectionChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _messages.Clear();
                        foreach (var msg in _diagnosticsService.Messages)
                        {
                            _messages.Add(new BootloaderMessageViewModel
                            {
                                Timestamp = msg.Timestamp,
                                Direction = msg.Direction,
                                CanId = msg.CanId,
                                Description = msg.Description,
                                DataHex = BitConverter.ToString(msg.Data).Replace("-", " ")
                            });
                        }
                    });
                };
                
                // Update errors when diagnostics service errors change
                // Note: Errors are added via RecordError method, we'll poll or use a timer
                var errorUpdateTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                errorUpdateTimer.Tick += (s, e) =>
                {
                    if (_diagnosticsService != null)
                    {
                        var currentErrors = _diagnosticsService.Errors.ToList();
                        if (currentErrors.Count != _errors.Count)
                        {
                            _errors.Clear();
                            foreach (var err in currentErrors)
                            {
                                _errors.Add(new BootloaderErrorViewModel
                                {
                                    Timestamp = err.Timestamp,
                                    ErrorCode = err.ErrorCode,
                                    Description = err.Description,
                                    SuggestedResolution = err.SuggestedResolution
                                });
                            }
                        }
                    }
                };
                errorUpdateTimer.Start();
            }
            
            // Subscribe to CAN service for message capture
            if (_canService != null)
            {
                _canService.MessageReceived += OnCANMessageReceived;
                // Subscribe to bootloader response events
                _canService.BootQueryResponseReceived += OnBootQueryResponseReceived;
                _canService.BootPingResponseReceived += OnBootPingResponseReceived;
                _canService.BootBeginResponseReceived += OnBootBeginResponseReceived;
                _canService.BootProgressReceived += OnBootProgressReceived;
                _canService.BootEndResponseReceived += OnBootEndResponseReceived;
                _canService.BootErrorReceived += OnBootErrorReceived;
            }
            
            // Initial UI update
            UpdateUI();
        }

        private void OnCANMessageReceived(CANMessage msg)
        {
            // Capture bootloader messages for diagnostics
            if (msg.ID >= 0x510 && msg.ID <= 0x51C)
            {
                // Determine if this is TX or RX based on message direction
                bool isTx = msg.Direction == "TX";
                _diagnosticsService?.CaptureMessage(msg.ID, msg.Data, isTx);
            }
        }

        private void UpdateUI()
        {
            // Update active bank display
            if (ActiveBankText != null)
            {
                ActiveBankText.Text = $"Active Bank: {_bootloaderInfo.ActiveBankName}";
            }

            // Update bootloader status
            if (BootloaderStatusText != null)
            {
                BootloaderStatusText.Text = _bootloaderInfo.IsPresent
                    ? $"Bootloader: {_bootloaderInfo.StatusDescription}"
                    : "Bootloader: Not Responding";
            }

            // Update bank information
            UpdateBankInfo(_bootloaderInfo.BankA, BankAVersionText, BankAStatusText, BankASizeText, BankACrcText, BankALastUpdateText);
            UpdateBankInfo(_bootloaderInfo.BankB, BankBVersionText, BankBStatusText, BankBSizeText, BankBCrcText, BankBLastUpdateText);

            // Update firmware version
            if (FirmwareVersionText != null)
            {
                FirmwareVersionText.Text = _bootloaderInfo.FirmwareVersion != null
                    ? $"Firmware: {_bootloaderInfo.FirmwareVersion}"
                    : "Firmware: Unknown";
            }

            // Update last update time
            if (LastUpdateText != null)
            {
                LastUpdateText.Text = _bootloaderInfo.LastUpdateTime.HasValue
                    ? $"Last Update: {_bootloaderInfo.LastUpdateTime.Value:yyyy-MM-dd HH:mm:ss}"
                    : "Last Update: Never";
            }
        }

        private void UpdateBankInfo(BankInfo bank, TextBlock? versionText, TextBlock? statusText, 
                                   TextBlock? sizeText, TextBlock? crcText, TextBlock? lastUpdateText)
        {
            if (versionText != null)
                versionText.Text = $"Version: {bank.VersionString}";
            if (statusText != null)
                statusText.Text = $"Status: {bank.StatusString}";
            if (sizeText != null)
                sizeText.Text = bank.Size > 0 ? $"Size: {bank.Size:N0} bytes" : "Size: Unknown";
            if (crcText != null)
                crcText.Text = bank.Crc > 0 ? $"CRC: 0x{bank.Crc:X8}" : "CRC: Unknown";
            if (lastUpdateText != null)
                lastUpdateText.Text = bank.LastUpdateTime.HasValue
                    ? $"Last Update: {bank.LastUpdateTime.Value:yyyy-MM-dd HH:mm:ss}"
                    : "Last Update: Never";
        }

        private void BrowseFirmwareBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Firmware Binary (*.bin)|*.bin|All Files (*.*)|*.*",
                    Title = "Select Firmware Binary"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    _selectedFirmwarePath = dialog.FileName;
                    var fileInfo = new FileInfo(_selectedFirmwarePath);
                    
                    if (FirmwareFilePathText != null)
                        FirmwareFilePathText.Text = Path.GetFileName(_selectedFirmwarePath);
                    
                    if (FirmwareFileSizeText != null)
                    {
                        long appSize = Math.Max(0, fileInfo.Length - 0x2000); // Subtract bootloader size
                        FirmwareFileSizeText.Text = $"{appSize:N0} bytes";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Firmware browse error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Failed to select firmware: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartFirmwareUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_firmwareUpdateService == null)
            {
                MessageBox.Show("Firmware update service not available.", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedFirmwarePath) || !File.Exists(_selectedFirmwarePath))
            {
                MessageBox.Show("Please select a firmware file first.", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_updateInProgress)
            {
                MessageBox.Show("Update already in progress.", "Warning", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Update firmware to {_bootloaderInfo.GetInactiveBankInfo().BankName}?\n\n" +
                $"File: {Path.GetFileName(_selectedFirmwarePath)}\n" +
                $"This will erase the inactive bank and write new firmware.",
                "Confirm Firmware Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            _updateInProgress = true;
            _updateCts = new CancellationTokenSource();
            _updateStartTime = DateTime.Now;
            _bytesSent = 0;
            _currentStep = BootloaderProcessStep.EnterBootloader;
            
            // Initialize progress tracking
            var fileInfo = new FileInfo(_selectedFirmwarePath);
            _totalBytes = Math.Max(0, fileInfo.Length - 0x2000);
            _totalChunks = (int)((_totalBytes + 6) / 7); // 7 bytes per chunk
            _currentSequenceNumber = 0;
            
            // Reset all step indicators
            ResetStepIndicators();
            
            // Initialize detailed progress display
            if (BytesTransferredText != null) BytesTransferredText.Text = $"0 / {_totalBytes:N0}";
            if (TimeElapsedText != null) TimeElapsedText.Text = "00:00";
            if (TimeRemainingText != null) TimeRemainingText.Text = "--:--";
            if (ChunksSentText != null) ChunksSentText.Text = $"0 / {_totalChunks}";
            if (SequenceNumberText != null) SequenceNumberText.Text = "0";
            
            StartFirmwareUpdateBtn.IsEnabled = false;
            CancelFirmwareUpdateBtn.IsEnabled = true;
            if (FirmwareProgressBar != null) FirmwareProgressBar.Value = 0;
            if (FirmwareProgressLabel != null) FirmwareProgressLabel.Text = "0%";
            UpdateStatusText("Entering bootloader mode...");
            
            // Log start of update
            LogOperation("Start Update", "TX", 0, "In Progress", $"File: {Path.GetFileName(_selectedFirmwarePath)}, Size: {_totalBytes:N0} bytes");
            
            // Update step indicator for Enter Bootloader
            UpdateStepIndicator(BootloaderProcessStep.EnterBootloader, false);

            var progress = new Progress<FirmwareProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (FirmwareProgressBar != null) FirmwareProgressBar.Value = p.Percentage;
                    if (FirmwareProgressLabel != null) 
                        FirmwareProgressLabel.Text = $"{p.Percentage:0}% ({p.ChunksSent}/{p.TotalChunks})";

                    // Calculate transfer rate and update detailed progress
                    var elapsed = DateTime.Now - _updateStartTime;
                    if (elapsed.TotalSeconds > 0)
                    {
                        _bytesSent = (long)(_totalBytes * p.Percentage / 100.0);
                        double bytesPerSecond = _bytesSent / elapsed.TotalSeconds;
                        string rateStr = bytesPerSecond > 1024 
                            ? $"{bytesPerSecond / 1024:F1} KB/s" 
                            : $"{bytesPerSecond:F0} B/s";
                        
                        if (FirmwareTransferRateText != null)
                            FirmwareTransferRateText.Text = rateStr;
                        
                        UpdateDetailedProgress(_bytesSent, (int)p.Percentage);
                    }
                    
                    // Update sequence number (approximate)
                    _currentSequenceNumber = (byte)(p.ChunksSent % 256);
                });
            });

            try
            {
                bool success = await _firmwareUpdateService.UpdateFirmwareAsync(
                    _selectedFirmwarePath, progress, _updateCts.Token);

                if (success)
                {
                    // Mark Reset step as complete (Reset command was sent, STM32 will reset)
                    UpdateStepIndicator(BootloaderProcessStep.Reset, true);
                    UpdateStepIndicator(BootloaderProcessStep.Complete, true);
                    _currentStep = BootloaderProcessStep.Complete;
                    LogOperation("Update Complete", "SYSTEM", 0, "Success", "Firmware update completed successfully - STM32 resetting");
                    UpdateStatusText("Update complete! System will reset.");
                    
                    MessageBox.Show("Firmware update completed successfully!\n\n" +
                                  "The system will reset and boot from the new firmware.",
                                  "Update Complete",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    UpdateStepIndicator(BootloaderProcessStep.Failed, false);
                    _currentStep = BootloaderProcessStep.Failed;
                    LogOperation("Update Failed", "SYSTEM", 0, "Failed", "Firmware update failed - check diagnostics");
                    UpdateStatusText("Update failed - check diagnostics");
                    
                    MessageBox.Show("Firmware update failed.\n\n" +
                                  "Check the diagnostics tab for error details.",
                                  "Update Failed",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                UpdateStepIndicator(BootloaderProcessStep.Failed, false);
                _currentStep = BootloaderProcessStep.Failed;
                LogOperation("Update Cancelled", "SYSTEM", 0, "Cancelled", "Firmware update was cancelled by user");
                UpdateStatusText("Update cancelled");
                
                MessageBox.Show("Firmware update was cancelled.", "Update Cancelled",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Firmware update error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Firmware update error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _updateInProgress = false;
                _currentStep = BootloaderProcessStep.Idle;
                _updateCts?.Dispose();
                _updateCts = null;
                StartFirmwareUpdateBtn.IsEnabled = true;
                CancelFirmwareUpdateBtn.IsEnabled = false;
                UpdateStatusText("Idle");
            }
        }

        private void CancelFirmwareUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            _updateCts?.Cancel();
        }

        private async void TestBootloaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_canService == null)
            {
                MessageBox.Show("CAN service not available.", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                TestBootloaderBtn.IsEnabled = false;
                TestBootloaderBtn.Content = "Testing...";
                
                _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>(), true);
                bool sent = _canService.SendMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>());
                
                if (sent)
                {
                    _currentStep = BootloaderProcessStep.Ping;
                    UpdateStepIndicator(_currentStep, false);
                    LogOperation("Ping", "TX", BootloaderProtocol.CanIdBootPing, "Sent", "Testing bootloader communication");
                    UpdateStatusText("Pinging bootloader...");
                    
                    // Wait up to 2 seconds for ping response
                    await Task.Delay(2000);
                    
                    if (_bootloaderInfo.Status == BootloaderStatus.Ready)
                    {
                        MessageBox.Show("Bootloader is responding!\n\nStatus: READY",
                                      "Test Successful",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No response from bootloader.\n\n" +
                                      "Possible causes:\n" +
                                      "• STM32 is not in bootloader mode\n" +
                                      "• CAN bus communication issue\n" +
                                      "• Bootloader not present",
                                      "Test Failed",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to send ping command.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Test bootloader error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestBootloaderBtn.IsEnabled = true;
                TestBootloaderBtn.Content = "Test Bootloader";
            }
        }

        private void EnterBootloaderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_canService == null)
            {
                MessageBox.Show("CAN service not available.", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                "Enter Bootloader Mode?\n\n" +
                "This will cause the STM32 to:\n" +
                "1. Set entry magic in RTC backup register\n" +
                "2. Reset immediately\n" +
                "3. Boot into bootloader mode\n\n" +
                "The system will be ready for firmware updates.",
                "Confirm Enter Bootloader",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                EnterBootloaderBtn.IsEnabled = false;
                EnterBootloaderBtn.Content = "Entering...";
                
                _diagnosticsService?.CaptureMessage(BootloaderProtocol.CanIdBootEnter, Array.Empty<byte>(), true);
                bool sent = _canService.RequestEnterBootloader();
                
                if (sent)
                {
                    _currentStep = BootloaderProcessStep.EnterBootloader;
                    UpdateStepIndicator(_currentStep, false);
                    LogOperation("Enter Bootloader", "TX", BootloaderProtocol.CanIdBootEnter, "Sent", "Requesting bootloader entry");
                    UpdateStatusText("Entering bootloader mode...");
                    
                    MessageBox.Show(
                        "Enter Bootloader command sent.\n\n" +
                        "STM32 will reset and enter bootloader mode.\n" +
                        "Wait a few seconds, then use 'Test Bootloader' to verify.",
                        "Command Sent",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to send Enter Bootloader command.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Enter bootloader error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EnterBootloaderBtn.IsEnabled = true;
                EnterBootloaderBtn.Content = "Enter Bootloader";
            }
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_canService == null)
            {
                MessageBox.Show("CAN service not available.", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                "Reset STM32?\n\n" +
                "This will cause the STM32 to reset immediately.\n" +
                "The system will boot from the active bank.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                bool sent = _canService.RequestReset();
                if (sent)
                {
                    MessageBox.Show("Reset command sent. STM32 will reset shortly.",
                                  "Reset Sent",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to send reset command.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reset error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchBankBtn_Click(object sender, RoutedEventArgs e)
        {
            var inactiveBank = _bootloaderInfo.GetInactiveBankInfo();
            
            if (!inactiveBank.IsValid)
            {
                MessageBox.Show(
                    $"Cannot switch to {inactiveBank.BankName}.\n\n" +
                    $"Reason: Bank is invalid or contains no firmware.\n\n" +
                    $"Please update firmware to {inactiveBank.BankName} first.",
                    "Cannot Switch Bank",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Switch active bank to {inactiveBank.BankName}?\n\n" +
                $"Current Active: {_bootloaderInfo.ActiveBankName}\n" +
                $"Target: {inactiveBank.BankName}\n" +
                $"Version: {inactiveBank.VersionString}\n\n" +
                $"Note: This requires a firmware update to the inactive bank.\n" +
                $"The bank switch happens automatically after a successful update.",
                "Switch Bank",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    $"To switch to {inactiveBank.BankName}, please:\n\n" +
                    $"1. Select a firmware file\n" +
                    $"2. Click 'Start Update'\n" +
                    $"3. The system will automatically switch banks after successful update\n\n" +
                    $"The inactive bank ({inactiveBank.BankName}) will become active.",
                    "How to Switch Bank",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void RollbackBtn_Click(object sender, RoutedEventArgs e)
        {
            var inactiveBank = _bootloaderInfo.GetInactiveBankInfo();
            
            if (!inactiveBank.IsValid)
            {
                MessageBox.Show(
                    $"Cannot rollback to {inactiveBank.BankName}.\n\n" +
                    $"Reason: Bank is invalid or contains no firmware.\n\n" +
                    $"Rollback is only possible if the inactive bank contains valid firmware.",
                    "Cannot Rollback",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Rollback to {inactiveBank.BankName}?\n\n" +
                $"Current Active: {_bootloaderInfo.ActiveBankName}\n" +
                $"Rollback Target: {inactiveBank.BankName}\n" +
                $"Version: {inactiveBank.VersionString}\n\n" +
                $"This will switch to the previous firmware version.\n" +
                $"Note: This requires a firmware update to trigger bank switch.",
                "Confirm Rollback",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    $"To rollback to {inactiveBank.BankName}, please:\n\n" +
                    $"1. Select a firmware file (or use the same version)\n" +
                    $"2. Click 'Start Update'\n" +
                    $"3. The system will switch to {inactiveBank.BankName} after update\n\n" +
                    $"Alternatively, if {inactiveBank.BankName} already has valid firmware,\n" +
                    $"you can trigger a switch by updating firmware.",
                    "How to Rollback",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void ValidateBanksBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_canService == null)
            {
                MessageBox.Show("CAN service not available.", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                ValidateBanksBtn.IsEnabled = false;
                ValidateBanksBtn.Content = "Validating...";
                
                // Query bootloader info to get current status
                bool sent = _canService.RequestBootloaderInfo();
                if (sent)
                {
                    await Task.Delay(2000); // Wait for response
                    
                    string message = "Bank Validation Results:\n\n";
                    message += $"Active Bank: {_bootloaderInfo.ActiveBankName}\n";
                    message += $"Bank A Status: {_bootloaderInfo.BankA.StatusString}\n";
                    if (_bootloaderInfo.BankA.IsValid)
                    {
                        message += $"  Version: {_bootloaderInfo.BankA.VersionString}\n";
                        message += $"  Size: {_bootloaderInfo.BankA.Size:N0} bytes\n";
                        message += $"  CRC: 0x{_bootloaderInfo.BankA.Crc:X8}\n";
                    }
                    message += $"\nBank B Status: {_bootloaderInfo.BankB.StatusString}\n";
                    if (_bootloaderInfo.BankB.IsValid)
                    {
                        message += $"  Version: {_bootloaderInfo.BankB.VersionString}\n";
                        message += $"  Size: {_bootloaderInfo.BankB.Size:N0} bytes\n";
                        message += $"  CRC: 0x{_bootloaderInfo.BankB.Crc:X8}\n";
                    }
                    
                    MessageBox.Show(message, "Bank Validation",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to request bootloader info.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Validate banks error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ValidateBanksBtn.IsEnabled = true;
                ValidateBanksBtn.Content = "Validate Banks";
            }
        }

        private async void QueryBootInfoBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_canService == null)
            {
                MessageBox.Show("CAN service not available.", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                QueryBootInfoBtn.IsEnabled = false;
                QueryBootInfoBtn.Content = "Querying...";
                
                bool sent = _canService.RequestBootloaderInfo();
                if (sent)
                {
                    LogOperation("Query Boot Info", "TX", BootloaderProtocol.CanIdBootQueryInfo, "Sent", "Requesting bootloader information");
                    // Wait up to 2 seconds for response
                    await Task.Delay(2000);
                    
                    if (_bootloaderInfo.IsPresent)
                    {
                        MessageBox.Show(
                            $"Bootloader Info:\n\n" +
                            $"Present: Yes\n" +
                            $"Version: {_bootloaderInfo.FirmwareVersion?.ToString() ?? "Unknown"}\n" +
                            $"Status: {_bootloaderInfo.StatusDescription}",
                            "Bootloader Info",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No response from bootloader. Ensure STM32 is in bootloader mode or application mode.",
                                      "No Response",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to request bootloader info.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Query boot info error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                QueryBootInfoBtn.IsEnabled = true;
                QueryBootInfoBtn.Content = "Query Info";
            }
        }

        private void ClearMessagesBtn_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsService?.ClearMessages();
            _messages.Clear();
        }

        private void ClearErrorsBtn_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsService?.ClearErrors();
            _errors.Clear();
        }

        private void ExportMessagesBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_diagnosticsService == null)
                {
                    MessageBox.Show("Diagnostics service not available.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Export CAN Messages",
                    FileName = $"bootloader_messages_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    string content = _diagnosticsService.ExportMessagesToText();
                    File.WriteAllText(dialog.FileName, content);
                    MessageBox.Show($"Messages exported to:\n{dialog.FileName}", "Export Complete",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export messages error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Failed to export messages: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnBootQueryResponseReceived(object? sender, BootQueryResponseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _bootloaderInfo.IsPresent = e.Present;
                if (e.Present)
                {
                    _bootloaderInfo.FirmwareVersion = new Version(e.Major, e.Minor, e.Patch);
                    _bootloaderInfo.Status = BootloaderStatus.Ready;
                    
                    // Update bank information from extended Query Response
                    _bootloaderInfo.ActiveBank = e.ActiveBank;
                    _bootloaderInfo.BankA.IsValid = (e.BankAValid == 0xFF);
                    _bootloaderInfo.BankB.IsValid = (e.BankBValid == 0xFF);
                    
                    LogOperation("Query Response", "RX", BootloaderProtocol.CanIdBootQueryResponse, "Success", 
                               $"Version: {e.Major}.{e.Minor}.{e.Patch}, Active: Bank {(e.ActiveBank == 0 ? 'A' : 'B')}, " +
                               $"Bank A: {(e.BankAValid == 0xFF ? "Valid" : "Invalid")}, " +
                               $"Bank B: {(e.BankBValid == 0xFF ? "Valid" : "Invalid")}");
                }
                else
                {
                    LogOperation("Query Response", "RX", BootloaderProtocol.CanIdBootQueryResponse, "Failed", "Bootloader not present");
                }
                UpdateUI();
            });
        }

        private void OnBootPingResponseReceived(object? sender, BootPingResponseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _bootloaderInfo.Status = BootloaderStatus.Ready;
                _bootloaderInfo.IsPresent = true;
                
                // If we were in EnterBootloader step, mark it complete (STM32 successfully entered bootloader mode)
                if (_currentStep == BootloaderProcessStep.EnterBootloader)
                {
                    UpdateStepIndicator(BootloaderProcessStep.EnterBootloader, true);
                    LogOperation("Enter Bootloader", "SYSTEM", BootloaderProtocol.CanIdBootEnter, "Success", "STM32 entered bootloader mode");
                }
                
                UpdateStepIndicator(BootloaderProcessStep.Ping, true);
                _currentStep = BootloaderProcessStep.Ping;
                LogOperation("Ping Response", "RX", BootloaderProtocol.CanIdBootPingResponse, "Success", "Bootloader ready");
                UpdateUI();
            });
        }

        private void OnBootBeginResponseReceived(object? sender, BootBeginResponseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Status == BootloaderStatus.InProgress)
                {
                    // Mark Ping as complete if we're transitioning from Ping to Begin
                    if (_currentStep == BootloaderProcessStep.Ping)
                    {
                        UpdateStepIndicator(BootloaderProcessStep.Ping, true);
                    }
                    
                    UpdateStepIndicator(BootloaderProcessStep.Begin, true);
                    _currentStep = BootloaderProcessStep.Transfer;
                    UpdateStepIndicator(_currentStep, false);
                    LogOperation("Begin Response", "RX", BootloaderProtocol.CanIdBootBeginResponse, "Success", $"Status: {e.Status}");
                    UpdateStatusText("Starting firmware transfer...");
                }
                else
                {
                    UpdateStepIndicator(BootloaderProcessStep.Begin, false);
                    _currentStep = BootloaderProcessStep.Failed;
                    LogOperation("Begin Response", "RX", BootloaderProtocol.CanIdBootBeginResponse, "Failed", $"Status: {e.Status}");
                    UpdateStatusText($"Update failed: {BootloaderProtocol.DescribeStatus(e.Status)}");
                }
                UpdateUI();
            });
        }

        private void OnBootProgressReceived(object? sender, BootProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _currentStep = BootloaderProcessStep.Transfer;
                UpdateStepIndicator(_currentStep, false);
                UpdateStatusText($"Transferring firmware: {e.Percent}% ({e.BytesReceived:N0} bytes)");
                LogOperation("Progress Update", "RX", BootloaderProtocol.CanIdBootProgress, "In Progress", 
                           $"{e.Percent}% - {e.BytesReceived:N0} bytes");
                UpdateDetailedProgress(e.BytesReceived, e.Percent);
            });
        }

        private void OnBootEndResponseReceived(object? sender, BootEndResponseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Status == BootloaderStatus.Success)
                {
                    // Mark Transfer as complete
                    UpdateStepIndicator(BootloaderProcessStep.Transfer, true);
                    
                    UpdateStepIndicator(BootloaderProcessStep.End, true);
                    _currentStep = BootloaderProcessStep.Reset;
                    UpdateStepIndicator(_currentStep, false);
                    LogOperation("End Response", "RX", BootloaderProtocol.CanIdBootEndResponse, "Success", "CRC validated, bank switched");
                    UpdateStatusText("Update complete! Resetting...");
                    
                    // Note: Reset step will be marked complete when FirmwareUpdateService completes
                    // (Reset command is sent but no response expected - STM32 resets immediately)
                }
                else
                {
                    UpdateStepIndicator(BootloaderProcessStep.End, false);
                    _currentStep = BootloaderProcessStep.Failed;
                    LogOperation("End Response", "RX", BootloaderProtocol.CanIdBootEndResponse, "Failed", 
                               $"Status: {BootloaderProtocol.DescribeStatus(e.Status)}");
                    UpdateStatusText($"Update failed: {BootloaderProtocol.DescribeStatus(e.Status)}");
                }
                UpdateUI();
            });
        }

        private void OnBootErrorReceived(object? sender, BootErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _currentStep = BootloaderProcessStep.Failed;
                UpdateStepIndicator(_currentStep, false);
                string details = $"Error: {BootloaderProtocol.DescribeStatus(e.ErrorCode)}";
                if (e.AdditionalData != 0)
                {
                    details += $", Additional: 0x{e.AdditionalData:X2}";
                }
                LogOperation("Error Response", "RX", BootloaderProtocol.CanIdBootError, "Failed", details);
                UpdateStatusText($"Error: {BootloaderProtocol.DescribeStatus(e.ErrorCode)}");
                UpdateUI();
            });
        }

        private void UpdateStepIndicator(BootloaderProcessStep step, bool completed)
        {
            Dispatcher.Invoke(() =>
            {
                Border? border = null;
                TextBlock? icon = null;
                TextBlock? status = null;
                
                switch (step)
                {
                    case BootloaderProcessStep.EnterBootloader:
                        border = Step1Border;
                        icon = Step1Icon;
                        status = Step1Status;
                        break;
                    case BootloaderProcessStep.Ping:
                        border = Step2Border;
                        icon = Step2Icon;
                        status = Step2Status;
                        break;
                    case BootloaderProcessStep.Begin:
                        border = Step3Border;
                        icon = Step3Icon;
                        status = Step3Status;
                        break;
                    case BootloaderProcessStep.Transfer:
                        border = Step4Border;
                        icon = Step4Icon;
                        status = Step4Status;
                        break;
                    case BootloaderProcessStep.End:
                        border = Step5Border;
                        icon = Step5Icon;
                        status = Step5Status;
                        break;
                    case BootloaderProcessStep.Reset:
                    case BootloaderProcessStep.Complete:
                        border = Step6Border;
                        icon = Step6Icon;
                        status = Step6Status;
                        break;
                }
                
                if (border != null && icon != null && status != null)
                {
                    if (completed)
                    {
                        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green
                        border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 142, 60));
                        icon.Text = "✓";
                        status.Text = "Complete";
                        status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                    }
                    else if (step == _currentStep)
                    {
                        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)); // Yellow
                        border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 124, 0));
                        icon.Text = "⏳";
                        status.Text = "In Progress";
                        status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
                    }
                    else if (step == BootloaderProcessStep.Failed)
                    {
                        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Red
                        border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 40, 40));
                        icon.Text = "✗";
                        status.Text = "Failed";
                        status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                    }
                }
            });
        }

        private void ResetStepIndicators()
        {
            Dispatcher.Invoke(() =>
            {
                var grayBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224));
                var grayBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
                
                foreach (var step in new[] { Step1Border, Step2Border, Step3Border, Step4Border, Step5Border, Step6Border })
                {
                    if (step != null)
                    {
                        step.Background = grayBrush;
                        step.BorderBrush = grayBorder;
                    }
                }
                
                foreach (var icon in new[] { Step1Icon, Step2Icon, Step3Icon, Step4Icon, Step5Icon, Step6Icon })
                {
                    if (icon != null) icon.Text = "⏳";
                }
                
                foreach (var status in new[] { Step1Status, Step2Status, Step3Status, Step4Status, Step5Status, Step6Status })
                {
                    if (status != null)
                    {
                        status.Text = "Pending";
                        status.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                    }
                }
            });
        }

        private void UpdateDetailedProgress(long bytesTransferred, int percent)
        {
            Dispatcher.Invoke(() =>
            {
                if (BytesTransferredText != null)
                    BytesTransferredText.Text = $"{bytesTransferred:N0} / {_totalBytes:N0}";
                
                var elapsed = DateTime.Now - _updateStartTime;
                if (TimeElapsedText != null)
                {
                    int totalSeconds = (int)elapsed.TotalSeconds;
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;
                    TimeElapsedText.Text = $"{minutes:D2}:{seconds:D2}";
                }
                
                if (TimeRemainingText != null && percent > 0 && percent < 100)
                {
                    double remainingSeconds = (elapsed.TotalSeconds / percent) * (100 - percent);
                    int remMinutes = (int)(remainingSeconds / 60);
                    int remSeconds = (int)(remainingSeconds % 60);
                    TimeRemainingText.Text = $"{remMinutes:D2}:{remSeconds:D2}";
                }
                else if (TimeRemainingText != null)
                {
                    TimeRemainingText.Text = "--:--";
                }
                
                if (ChunksSentText != null)
                {
                    int chunksSent = (int)((bytesTransferred + 6) / 7);
                    ChunksSentText.Text = $"{chunksSent} / {_totalChunks}";
                }
                
                if (SequenceNumberText != null)
                    SequenceNumberText.Text = _currentSequenceNumber.ToString();
            });
        }

        private void UpdateStatusText(string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (FirmwareStatusText != null)
                    FirmwareStatusText.Text = $"Status: {status}";
                
                // Update status icon
                if (StatusIcon != null)
                {
                    if (status.Contains("complete") || status.Contains("Complete") || status.Contains("success"))
                    {
                        StatusIcon.Text = "✓";
                        StatusIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                    }
                    else if (status.Contains("fail") || status.Contains("Fail") || status.Contains("error") || status.Contains("Error"))
                    {
                        StatusIcon.Text = "✗";
                        StatusIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
                    }
                    else if (status.Contains("Idle") || status.Contains("idle"))
                    {
                        StatusIcon.Text = "○";
                        StatusIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                    }
                    else
                    {
                        StatusIcon.Text = "⏳";
                        StatusIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                    }
                }
            });
        }

        private void LogOperation(string operation, string direction, uint canId, string status, string details)
        {
            Dispatcher.Invoke(() =>
            {
                _operationLog.Add(new BootloaderOperation
                {
                    Timestamp = DateTime.Now,
                    Operation = operation,
                    Direction = direction,
                    CanId = canId,
                    Status = status,
                    Details = details
                });
                
                // Auto-scroll to latest entry
                if (OperationLogDataGrid != null && _operationLog.Count > 0)
                {
                    OperationLogDataGrid.ScrollIntoView(_operationLog[_operationLog.Count - 1]);
                }
            });
        }

        private void ClearOperationLogBtn_Click(object sender, RoutedEventArgs e)
        {
            _operationLog.Clear();
        }

        private void ExportOperationLogBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Export Operation Log",
                    FileName = $"bootloader_operations_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var content = new System.Text.StringBuilder();
                    content.AppendLine("Bootloader Operation Log");
                    content.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    content.AppendLine(new string('=', 80));
                    content.AppendLine();
                    
                    foreach (var op in _operationLog)
                    {
                        content.AppendLine($"[{op.Timestamp:HH:mm:ss.fff}] {op.Direction} {op.Operation} (0x{op.CanId:X3})");
                        content.AppendLine($"  Status: {op.Status}");
                        content.AppendLine($"  Details: {op.Details}");
                        content.AppendLine();
                    }
                    
                    File.WriteAllText(dialog.FileName, content.ToString());
                    MessageBox.Show($"Operation log exported to:\n{dialog.FileName}", "Export Complete",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export operation log error: {ex.Message}", "BootloaderManager");
                MessageBox.Show($"Failed to export operation log: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInProgress)
            {
                var result = MessageBox.Show(
                    "Firmware update is in progress. Close anyway?",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                _updateCts?.Cancel();
            }

            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_updateInProgress)
            {
                e.Cancel = true;
                CloseBtn_Click(this, new RoutedEventArgs());
            }
            
            // Unsubscribe from events
            if (_canService != null)
            {
                _canService.BootQueryResponseReceived -= OnBootQueryResponseReceived;
                _canService.BootPingResponseReceived -= OnBootPingResponseReceived;
                _canService.BootBeginResponseReceived -= OnBootBeginResponseReceived;
                _canService.BootProgressReceived -= OnBootProgressReceived;
                _canService.BootEndResponseReceived -= OnBootEndResponseReceived;
                _canService.BootErrorReceived -= OnBootErrorReceived;
                _canService.MessageReceived -= OnCANMessageReceived;
            }
            
            base.OnClosing(e);
        }
    }

    // View models for data binding
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
        public byte ErrorCode { get; set; }
        public string Description { get; set; } = "";
        public string SuggestedResolution { get; set; } = "";
    }

    public class BootloaderOperation
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = "";
        public string Direction { get; set; } = ""; // TX/RX
        public uint CanId { get; set; }
        public string Status { get; set; } = ""; // Success/Failed/Timeout
        public string Details { get; set; } = "";
    }
}

