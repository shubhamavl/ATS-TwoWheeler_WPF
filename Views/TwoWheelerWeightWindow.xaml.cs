using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using Microsoft.Win32;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class TwoWheelerWeightWindow : Window
    {
        // Data collection for graph series (total weight only)
        private readonly ObservableCollection<double> _totalValues = new();

        // Event-driven data collection (thread-safe)
        private readonly ConcurrentQueue<double> _pendingWeights = new();
        private readonly ConcurrentQueue<DateTime> _pendingTimestamps = new();

        // Timers
        private DispatcherTimer? _uiTimer;      // UI update timer (20 Hz)

        // Services
        private readonly CANService? _canService;
        private readonly WeightProcessor? _weightProcessor;
        private string _unitLabel = "kg"; // Default to kg (Weight Mode)

        // Chart control
        private CartesianChart _axleChart = null!;

        // State
        private bool _isPaused = false;
        private const int MAX_SAMPLES = 21000;    // Sliding window size for data display
        private int _sampleCount = 0;
        private bool _isBrakeMode = false;

        // Test state management
        private enum TestState { Idle, Reading, Stopped, Completed }
        private TestState _testState = TestState.Idle;

        // Test data tracking
        private AxleTestDataModel? _currentTestData;
        private double _minWeight = double.MaxValue;
        private double _maxWeight = double.MinValue;
        private bool _hasMinMaxData = false;
        private int _testSampleCount = 0;
        private DateTime? _testStartTime = null;

        // Data rate monitoring
        private int _dataPointsCollected = 0;
        private DateTime _lastRateCheck = DateTime.Now;

        // Connection status update throttling
        private DateTime _lastConnectionStatusUpdate = DateTime.MinValue;
        private const int CONNECTION_STATUS_UPDATE_INTERVAL_MS = 500;

        // Validation thresholds
        private const double MIN_VALID_WEIGHT = 10.0; // kg - minimum valid weight

        public TwoWheelerWeightWindow(CANService? canService, WeightProcessor? weightProcessor)
        {
            InitializeComponent();
            _canService = canService;
            _weightProcessor = weightProcessor;

            InitializeGraph();
            InitializeTimers();
            UpdateConnectionStatus();

            // Subscribe to CAN service events
            if (_canService != null)
            {
                _canService.MessageReceived += CanService_MessageReceived;
                _canService.RawDataReceived += OnRawDataReceived;
            }

            // Set initial state
            UpdateTestState(TestState.Idle);
        }


        private void OnRawDataReceived(object? sender, RawDataEventArgs e)
        {
            // Event-driven data collection - enqueue weights for processing
            if (_testState == TestState.Reading && _weightProcessor != null)
            {
                double totalWeight = _weightProcessor.LatestTotal.TaredWeight;

                _pendingWeights.Enqueue(totalWeight);
                _pendingTimestamps.Enqueue(DateTime.Now);
            }
        }

        private void InitializeGraph()
        {
            // Create chart control programmatically
            _axleChart = new CartesianChart();
            chartContainer.Children.Add(_axleChart);

            // Configure single series: Total Weight
            _axleChart.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _totalValues,
                    Name = "Total Weight",
                    Fill = null,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(new SKColor(39, 174, 96), 2), // Green
                    LineSmoothness = 0
                }
            };

            // X-Axis Configuration (Samples)
            _axleChart.XAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = MAX_SAMPLES,
                    Name = "Samples",
                    TextSize = 12
                }
            };

            // Y-Axis Configuration (Weight in kg)
            _axleChart.YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 2000, // Axle weight range (adjust as needed)
                    Name = "Weight (kg)",
                    TextSize = 12
                }
            };
        }

        private void InitializeTimers()
        {
            // UI Update Timer (20 Hz - 50ms) - Batch processes pending data on UI thread
            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(50);
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (_isPaused && _testState != TestState.Reading)
                return;

            try
            {
                // Calculate data rate every second
                var now = DateTime.Now;
                if ((now - _lastRateCheck).TotalSeconds >= 1.0)
                {
                    if (DataRateText != null)
                    {
                        DataRateText.Text = $"Rate: {_dataPointsCollected} pts/sec";
                    }
                    _dataPointsCollected = 0;
                    _lastRateCheck = now;
                }

                // Batch dequeue from ConcurrentQueue (thread-safe, non-blocking)
                var batchWeights = new List<double>();
                var batchTimestamps = new List<DateTime>();

                while (_pendingWeights.TryDequeue(out double weight) && _pendingTimestamps.TryDequeue(out DateTime timestamp))
                {
                    batchWeights.Add(weight);
                    batchTimestamps.Add(timestamp);
                }

                // Process batch data
                if (batchWeights.Count > 0 && _testState == TestState.Reading)
                {
                    ProcessBatchData(batchWeights);
                }

                // Update status displays (numeric values, validation, etc.)
                UpdateStatusDisplays();

                // Auto-scroll X-axis only when count changes
                int maxCount = _totalValues.Count;
                if (maxCount != _sampleCount && maxCount > 100)
                {
                    var xAxis = _axleChart.XAxes.First();
                    xAxis.MinLimit = Math.Max(0, maxCount - MAX_SAMPLES);
                    xAxis.MaxLimit = maxCount;
                    _sampleCount = maxCount;
                }
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Two Wheeler UI update error: {ex.Message}", "TwoWheeler");
            }
        }

        private void ProcessBatchData(List<double> weights)
        {
            if (_weightProcessor == null) return;

            double totalWeight = _weightProcessor.LatestTotal.TaredWeight;

            // Add to graph collection (total weight only)
            _totalValues.Add(totalWeight);

            // Track Min/Max during test
            if (_testState == TestState.Reading)
            {
                if (totalWeight > 0)
                {
                    // Peak Hold Logic (Enhanced)
                    // Reset peak if starting new test is handled in StartTest()
                    
                    if (!_hasMinMaxData || totalWeight < _minWeight)
                        _minWeight = totalWeight;
                    if (!_hasMinMaxData || totalWeight > _maxWeight)
                        _maxWeight = totalWeight;
                        
                    _hasMinMaxData = true;
                }

                _testSampleCount++;
                _dataPointsCollected++;
            }

            // Maintain sliding window
            if (_totalValues.Count > MAX_SAMPLES)
                _totalValues.RemoveAt(0);
        }


        private void UpdateStatusDisplays()
        {
            try
            {
                double totalWeight = 0;
                if (_weightProcessor != null)
                {
                    // Total weight only for ATS Two-Wheeler
                    totalWeight = _weightProcessor.LatestTotal.TaredWeight;
                }

                double total = totalWeight;
                
                // Normalize negative zero to positive zero (avoid displaying "-0.0 kg")
                if (totalWeight == 0.0)
                {
                    totalWeight = 0.0; // Force positive zero
                }

                // Update weight displays (total weight only)
                if (TotalWeightText != null)
                    TotalWeightText.Text = $"{totalWeight:F1} {_unitLabel}";
                if (MainWeightText != null)
                    MainWeightText.Text = $"{totalWeight:F1} {_unitLabel}";

                // Update sample count (use test sample count if test is active)
                if (SampleCountText != null)
                {
                    SampleCountText.Text = _testState == TestState.Reading ? _testSampleCount.ToString() : _sampleCount.ToString();
                }

                // Update Min/Max displays
                if (MinWeightText != null && MaxWeightText != null)
                {
                    if (_hasMinMaxData)
                    {
                        // Normalize negative zero to positive zero
                        double minWeight = _minWeight == 0.0 ? 0.0 : _minWeight;
                        double maxWeight = _maxWeight == 0.0 ? 0.0 : _maxWeight;
                        
                        MinWeightText.Text = $"{minWeight:F1} {_unitLabel}";
                        MaxWeightText.Text = $"{maxWeight:F1} {_unitLabel}";
                    }
                    else
                    {
                        MinWeightText.Text = $"-- {_unitLabel}";
                        MaxWeightText.Text = $"-- {_unitLabel}";
                    }
                }

                // Update validation indicators (using total weight)
                UpdateValidationIndicators(totalWeight);
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Two Wheeler status update error: {ex.Message}", "TwoWheeler");
            }
        }

        private void UpdateValidationIndicators(double totalWeight)
        {
            // Validation (Green >= 10kg, Red < 10kg)
            var brush = totalWeight >= MIN_VALID_WEIGHT
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))  // Green
                : new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

            if (ValidationIndicatorRect != null)
                ValidationIndicatorRect.Background = brush;
        }

        private void UpdateConnectionStatus()
        {
            try
            {
                bool isConnected = _canService?.IsConnected ?? false;

                if (ConnectionIndicator != null)
                {
                    ConnectionIndicator.Fill = isConnected
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)) // Green
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)); // Red
                }

                if (ConnectionStatusText != null)
                {
                    ConnectionStatusText.Text = isConnected ? "Connected" : "Disconnected";
                }
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Two Wheeler connection status error: {ex.Message}", "TwoWheeler");
            }
        }

        private void CanService_MessageReceived(Models.CANMessage message)
        {
            // Throttle connection status updates to prevent excessive UI updates
            var now = DateTime.Now;
            if ((now - _lastConnectionStatusUpdate).TotalMilliseconds >= CONNECTION_STATUS_UPDATE_INTERVAL_MS)
            {
                _lastConnectionStatusUpdate = now;
                Dispatcher.BeginInvoke(() => UpdateConnectionStatus());
            }
        }

        // Manual Test Controls
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            StartTest();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            StopTest();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveTestData();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Keyboard shortcuts: F1=Start, F2=Stop, F3=Save
            switch (e.Key)
            {
                case Key.F1:
                    if (StartBtn != null && StartBtn.IsEnabled)
                        StartTest();
                    e.Handled = true;
                    break;
                case Key.F2:
                    if (StopBtn != null && StopBtn.IsEnabled)
                        StopTest();
                    e.Handled = true;
                    break;
                case Key.F3:
                    if (SaveBtn != null && SaveBtn.IsEnabled)
                        SaveTestData();
                    e.Handled = true;
                    break;
            }
        }

        private void StartTest()
        {
            try
            {
                _testStartTime = DateTime.Now;
                _testSampleCount = 0;
                _minWeight = double.MaxValue;
                _maxWeight = double.MinValue;
                _hasMinMaxData = false;

                _currentTestData = new AxleTestDataModel
                {
                    TestId = Guid.NewGuid().ToString(),
                    AxleNumber = 1, // Single axle for Two Wheeler system
                    TestStartTime = _testStartTime.Value
                };

                UpdateTestState(TestState.Reading);
                ProductionLogger.Instance.LogInfo("Two Wheeler weight test started", "TwoWheeler");
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Error starting test: {ex.Message}", "TwoWheeler");
                MessageBox.Show($"Error starting test: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopTest()
        {
            try
            {
                if (_testState == TestState.Reading)
                {
                    UpdateTestState(TestState.Stopped);
                    ProductionLogger.Instance.LogInfo("Two Wheeler weight test stopped", "TwoWheeler");
                }
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Error stopping test: {ex.Message}", "TwoWheeler");
            }
        }

        private void SaveTestData()
        {
            try
            {
                if (_currentTestData == null || _weightProcessor == null)
                {
                    MessageBox.Show("No test data to save. Please start a test first.", "No Data",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get current weight
                double totalWeight = _weightProcessor.LatestTotal.TaredWeight;

                // Update test data (total weight only)
                _currentTestData.TotalWeight = totalWeight;
                _currentTestData.TestEndTime = DateTime.Now;
                _currentTestData.SampleCount = _testSampleCount;
                _currentTestData.MinWeight = _hasMinMaxData ? _minWeight : 0;
                _currentTestData.MaxWeight = _hasMinMaxData ? _maxWeight : 0;

                // Validation status
                _currentTestData.ValidationStatus = totalWeight >= MIN_VALID_WEIGHT ? "Pass" : "Fail";

                // Save to JSON file
                SaveTestDataToJson(_currentTestData);

                UpdateTestState(TestState.Completed);
                ProductionLogger.Instance.LogInfo($"Two Wheeler test data saved: Total={totalWeight:F1}kg", "TwoWheeler");

                MessageBox.Show(
                    $"Test data saved successfully!\n\n" +
                    $"Total Weight: {totalWeight:F1} kg\n" +
                    $"Validation: {_currentTestData.ValidationStatus}\n" +
                    $"Min: {_currentTestData.MinWeight:F1} kg\n" +
                    $"Max: {_currentTestData.MaxWeight:F1} kg\n" +
                    $"Samples: {_testSampleCount}",
                    "Test Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Error saving test data: {ex.Message}", "TwoWheeler");
                MessageBox.Show($"Error saving test data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTestDataToJson(AxleTestDataModel testData)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FileName = $"AxleTest_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    string json = JsonSerializer.Serialize(testData, options);
                    File.WriteAllText(saveDialog.FileName, json);

                    ProductionLogger.Instance.LogInfo($"Test data saved to: {saveDialog.FileName}", "TwoWheeler");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save JSON file: {ex.Message}", ex);
            }
        }

        private void UpdateTestState(TestState newState)
        {
            _testState = newState;

            Dispatcher.Invoke(() =>
            {
                // Update button states
                if (StartBtn != null)
                    StartBtn.IsEnabled = (newState == TestState.Idle || newState == TestState.Completed);
                if (StopBtn != null)
                    StopBtn.IsEnabled = (newState == TestState.Reading);
                if (SaveBtn != null)
                    SaveBtn.IsEnabled = (newState == TestState.Stopped || newState == TestState.Completed);

                // Update status message
                if (StatusMessageText != null)
                {
                    switch (newState)
                    {
                        case TestState.Idle:
                            StatusMessageText.Text = "Ready to start axle weight test";
                            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
                            break;
                        case TestState.Reading:
                            StatusMessageText.Text = "Reading axle weight...";
                            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
                            break;
                        case TestState.Stopped:
                            StatusMessageText.Text = "Test stopped. Click Save to save data.";
                            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                            break;
                        case TestState.Completed:
                            StatusMessageText.Text = "Test completed and saved. Ready for next test.";
                            StatusMessageText.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
                            break;
                    }
                }
            });
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = true;
            if (PauseBtn != null) PauseBtn.IsEnabled = false;
            if (ResumeBtn != null) ResumeBtn.IsEnabled = true;
            ProductionLogger.Instance.LogInfo("Two Wheeler graph paused", "TwoWheeler");
        }

        private void ResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
            if (PauseBtn != null) PauseBtn.IsEnabled = true;
            if (ResumeBtn != null) ResumeBtn.IsEnabled = false;
            ProductionLogger.Instance.LogInfo("Two Wheeler graph resumed", "TwoWheeler");
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear graph data
                _totalValues.Clear();
                
                // Clear queues
                while (_pendingWeights.TryDequeue(out _)) { }
                while (_pendingTimestamps.TryDequeue(out _)) { }

                // Reset tracking
                _sampleCount = 0;
                _testSampleCount = 0;
                _minWeight = double.MaxValue;
                _maxWeight = double.MinValue;
                _hasMinMaxData = false;
                _currentTestData = null;
                _testStartTime = null;

                // Reset X-axis
                var xAxis = _axleChart.XAxes.First();
                xAxis.MinLimit = 0;
                xAxis.MaxLimit = MAX_SAMPLES;

                // Reset UI
                if (SampleCountText != null) SampleCountText.Text = "0";
                if (MinWeightText != null) MinWeightText.Text = "-- kg";
                if (MaxWeightText != null) MaxWeightText.Text = "-- kg";
                if (MainWeightText != null) MainWeightText.Text = "-- kg";
                if (TotalWeightText != null) TotalWeightText.Text = "-- kg";

                // Reset validation indicators
                if (ValidationIndicatorRect != null)
                    ValidationIndicatorRect.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

                UpdateTestState(TestState.Idle);
                ProductionLogger.Instance.LogInfo("Two Wheeler graph cleared", "TwoWheeler");
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Two Wheeler clear error: {ex.Message}", "TwoWheeler");
            }
        }

        private async void BrakeModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggle)
            {
                bool targetMode = toggle.IsChecked ?? false;
                
                // UX: Disable toggle momentarily to indicate "Switching" (Mechanical Relay Settling)
                toggle.IsEnabled = false;
                toggle.Content = "⏳ Switching...";
                
                // UX: Wait 1 second for relay settling (visual feedback only, firmware handles the 20ms safety)
                await Task.Delay(1000);
                
                bool success = true;
                _isBrakeMode = targetMode;
                // Switch unit label: Weight=kg, Brake=N
                _unitLabel = _isBrakeMode ? "N" : "kg";
                
                try 
                {
                    // 1. Switch firmware mode (0x050)
                    _canService?.SwitchSystemMode(_isBrakeMode);
                    
                    // 2. Switch calibration in WeightProcessor
                    // Note: If calibrated with Newtons, values will be Newtons. If Kg, then Kg.
                    if (_weightProcessor != null)
                    {
                        _weightProcessor.SetBrakeMode(_isBrakeMode);
                    }
                    
                    // 3. Update window title or status
                    StatusMessageText.Text = _isBrakeMode ? "Brake Force Mode Active" : "Weight Mode Active";
                    
                    ProductionLogger.Instance.LogInfo($"Switched to {(_isBrakeMode ? "Brake" : "Weight")} mode", "TwoWheeler");
                }
                catch (Exception ex)
                {
                    ProductionLogger.Instance.LogError($"Error switching mode: {ex.Message}", "TwoWheeler");
                    MessageBox.Show($"Error switching mode: {ex.Message}", "Error");
                    success = false;
                }
                finally
                {
                    // Re-enable toggle
                    toggle.IsEnabled = true;
                    
                    if (success)
                    {
                        // Update visual state based on mode
                        toggle.Content = _isBrakeMode ? "🛑 Brake Mode (ON)" : "🛑 Brake Mode";
                        toggle.Background = _isBrakeMode 
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC3545")) // Red for ON
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6C757D")); // Grey for OFF
                    }
                    else
                    {
                        // Revert if failed
                        _isBrakeMode = !targetMode;
                        toggle.IsChecked = _isBrakeMode;
                        toggle.Content = _isBrakeMode ? "🛑 Brake Mode (ON)" : "🛑 Brake Mode";
                    }
                }
            }
        }

        private void CalibrateBtn_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                byte currentMode = _canService?.CurrentADCMode ?? 0;
                var calibrationDialog = new CalibrationDialog(currentMode, 500, _isBrakeMode);
                calibrationDialog.Owner = this;
                
                if (calibrationDialog.ShowDialog() == true)
                {
                    // Reload calibration in WeightProcessor
                    _weightProcessor?.LoadCalibration();
                    
                    // Reset filters
                    _weightProcessor?.ResetFilters();
                    
                    ProductionLogger.Instance.LogInfo($"Calibration updated from dialog (BrakeMode={_isBrakeMode})", "TwoWheeler");
                }
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Error opening calibration: {ex.Message}", "TwoWheeler");
                MessageBox.Show($"Error opening calibration: {ex.Message}", "Error");
            }
        }

        private async void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var exportBtn = sender as System.Windows.Controls.Button;
            if (exportBtn == null) return;

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"TwoWheelerGraph_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Disable button during export
                    exportBtn.IsEnabled = false;
                    exportBtn.Content = "💾 Exporting...";

                    try
                    {
                        await ExportToCSVAsync(saveDialog.FileName);
                        MessageBox.Show("Two Wheeler graph data exported successfully!", "Export Complete",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    finally
                    {
                        // Re-enable button
                        exportBtn.IsEnabled = true;
                        exportBtn.Content = "💾 Export CSV";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ProductionLogger.Instance.LogError($"Two Wheeler export error: {ex.Message}", "TwoWheeler");

                // Re-enable button on error
                if (exportBtn != null)
                {
                    exportBtn.IsEnabled = true;
                    exportBtn.Content = "💾 Export CSV";
                }
            }
        }

        private async Task ExportToCSVAsync(string filePath)
        {
            // Run file writing on background thread to avoid blocking UI
            await Task.Run(() =>
            {
                try
                {
                    using var writer = new System.IO.StreamWriter(filePath);

                    // Write header
                    writer.WriteLine("Sample,Total Weight (kg)");

                    // Copy data to array for thread-safe access
                    double[] totalData = _totalValues.ToArray();

                    // Write data
                    for (int i = 0; i < totalData.Length; i++)
                    {
                        writer.WriteLine($"{i},{totalData[i]:F2}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to write CSV file: {ex.Message}", ex);
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Stop UI timer
                _uiTimer?.Stop();

                // Unsubscribe from events
                if (_canService != null)
                {
                    _canService.MessageReceived -= CanService_MessageReceived;
                    _canService.RawDataReceived -= OnRawDataReceived;
                }

                // Clear queues
                while (_pendingWeights.TryDequeue(out _)) { }
                while (_pendingTimestamps.TryDequeue(out _)) { }

                ProductionLogger.Instance.LogInfo("TwoWheelerWeightWindow closed", "TwoWheeler");
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Two Wheeler window close error: {ex.Message}", "TwoWheeler");
            }

            base.OnClosed(e);
        }

    }
}


