using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public enum TestState
    {
        Idle,
        Running,
        Paused,
        Stopping
    }

    public class TwoWheelerWeightViewModel : BaseViewModel
    {
        private const int MaxGraphSamples = 500;

        private readonly ICANService _canService;
        private readonly IWeightProcessorService _weightProcessor;
        private readonly IDataLoggerService _dataLogger;
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialogService;

        // Chart Data
        private readonly ObservableCollection<double> _totalWeightValues = new();
        public ReadOnlyObservableCollection<double> TotalWeightValues { get; }

        public ISeries[] Series { get; }

        private Axis[] _xAxes = Array.Empty<Axis>();
        public Axis[] XAxes
        {
            get => _xAxes;
            set => SetProperty(ref _xAxes, value);
        }

        private Axis[] _yAxes = Array.Empty<Axis>();
        public Axis[] YAxes
        {
            get => _yAxes;
            set => SetProperty(ref _yAxes, value);
        }

        private TestState _currentState = TestState.Idle;
        public TestState CurrentState
        {
            get => _currentState;
            set 
            {
                if (SetProperty(ref _currentState, value))
                {
                    OnPropertyChanged(nameof(StatusDescription));
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanStop));
                }
            }
        }

        public string StatusDescription => CurrentState switch
        {
            TestState.Idle => "Ready to Test",
            TestState.Running => "Test in Progress...",
            TestState.Paused => "Test Paused",
            TestState.Stopping => "Stopping...",
            _ => "Unknown"
        };

        public bool IsRunning => CurrentState == TestState.Running;
        public bool CanStart => CurrentState == TestState.Idle || CurrentState == TestState.Paused;
        public bool CanStop => CurrentState == TestState.Running || CurrentState == TestState.Paused;

        // Formatted strings for UI
        public string MainWeightText => IsCalibrated ? $"{CurrentWeight:F1} {UnitLabel}" : "Calibrate first";
        public string TotalWeightText => IsCalibrated ? $"{CurrentWeight:F1} {UnitLabel}" : "Calibrate first";
        public string MinWeightText => IsCalibrated ? $"{MinWeight:F1} {UnitLabel}" : "---";
        public string MaxWeightText => IsCalibrated ? $"{MaxWeight:F1} {UnitLabel}" : "---";
        public string DataRateText => $"Rate: {_dataPointsPerSec} pts/sec";
        
        // Status & Connection
        public string ConnectionStatus => _canService.IsConnected ? "Connected" : "Disconnected";
        public string ConnectionColor => _canService.IsConnected ? "#FF28A745" : "#FFDC3545"; // Green / Red
        
        private string _validationColor = "#FFDC3545"; // Default Red
        public string ValidationColor
        {
            get => _validationColor;
            set => SetProperty(ref _validationColor, value);
        }

        private int _dataPointsPerSec;
        public int DataPointsPerSec
        {
            get => _dataPointsPerSec;
            set => SetProperty(ref _dataPointsPerSec, value);
        }
        private DateTime _lastRateCheck = DateTime.Now;
        private int _dataPointsCurrentSec;

        private double _currentWeight;
        public double CurrentWeight
        {
            get => _currentWeight;
            set => SetProperty(ref _currentWeight, value);
        }

        private string _timerText = "00:00:00";
        public string TimerText
        {
            get => _timerText;
            set => SetProperty(ref _timerText, value);
        }

        private double _maxWeight;
        public double MaxWeight
        {
            get => _maxWeight;
            set => SetProperty(ref _maxWeight, value);
        }

        private double _minWeight;
        public double MinWeight
        {
            get => _minWeight;
            set => SetProperty(ref _minWeight, value);
        }

        private DateTime _lastTimerTick;
        private TimeSpan _accumulatedTime = TimeSpan.Zero;
        private DispatcherTimer? _testTimer;
        private int _sampleCount;
        public int SampleCount
        {
            get => _sampleCount;
            set => SetProperty(ref _sampleCount, value);
        }

        private bool _isCalibrated = true;
        public bool IsCalibrated
        {
            get => _isCalibrated;
            set => SetProperty(ref _isCalibrated, value);
        }

        private string _unitLabel = "kg";
        public string UnitLabel
        {
            get => _unitLabel;
            set => SetProperty(ref _unitLabel, value);
        }

        private bool _isBrakeMode;
        public bool IsBrakeMode
        {
            get => _isBrakeMode;
            set 
            {
                if (SetProperty(ref _isBrakeMode, value))
                {
                    _canService.SwitchSystemMode(_isBrakeMode ? SystemMode.Brake : SystemMode.Weight);
                    
                    if (_isBrakeMode)
                    {
                        UnitLabel = _settings.Settings.BrakeDisplayUnit;
                        YAxes[0].Name = UnitLabel == "N" ? "Brake Force (N)" : "Brake Force (kg)";
                    }
                    else
                    {
                        UnitLabel = "kg";
                        YAxes[0].Name = "Weight (kg)";
                    }

                    OnPropertyChanged(nameof(MainWeightText));
                    OnPropertyChanged(nameof(TotalWeightText));
                    OnPropertyChanged(nameof(MinWeightText));
                    OnPropertyChanged(nameof(MaxWeightText));
                }
            }
        }

        private readonly ConcurrentQueue<double> _stabilityBuffer = new();
        private const int StabilityBufferSize = 10;
        
        // Commands
        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand PauseTestCommand { get; }
        public ICommand SaveTestCommand { get; }
        public ICommand ClearDataCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand SwitchSystemModeCommand { get; }
        public ICommand ResumeTestCommand { get; }

        public TwoWheelerWeightViewModel(ICANService canService, IWeightProcessorService weightProcessor, IDataLoggerService dataLogger, ISettingsService settings, IDialogService dialogService)
        {
            _canService = canService;
            _weightProcessor = weightProcessor;
            _dataLogger = dataLogger;
            _settings = settings;
            _dialogService = dialogService;

            TotalWeightValues = new ReadOnlyObservableCollection<double>(_totalWeightValues);

            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _totalWeightValues,
                    Name = "Total Weight",
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 },
                    Fill = null,
                    GeometrySize = 0
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Name = "Samples",
                    NamePaint = new SolidColorPaint(SKColors.Black),
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 12,
                    MinLimit = 0,
                    MaxLimit = MaxGraphSamples
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    Name = "Weight (kg)",
                    NamePaint = new SolidColorPaint(SKColors.Black),
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 12
                }
            };

            StartTestCommand = new RelayCommand(_ => StartTest(), _ => CanStart);
            StopTestCommand = new RelayCommand(_ => StopTest(), _ => CanStop);
            PauseTestCommand = new RelayCommand(_ => PauseTest(), _ => IsRunning);
            ResumeTestCommand = new RelayCommand(_ => ResumeTest(), _ => CurrentState == TestState.Paused);
            SaveTestCommand = new RelayCommand(_ => SaveTest());
            ClearDataCommand = new RelayCommand(_ => ClearData());
            ExportCommand = new RelayCommand(async _ => await ExportDataAsync());
            SwitchSystemModeCommand = new RelayCommand(_ => IsBrakeMode = !IsBrakeMode);
        }

        private void ResumeTest()
        {
            CurrentState = TestState.Running;
            _lastTimerTick = DateTime.Now;
        }

        private async Task ExportDataAsync()
        {
            try
            {
                string? filePath = _dialogService.ShowSaveFileDialog("CSV files (*.csv)|*.csv|All files (*.*)|*.*", $"TwoWheelerGraph_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                
                if (string.IsNullOrEmpty(filePath)) return;

                await Task.Run(() =>
                {
                    using var writer = new System.IO.StreamWriter(filePath);
                    writer.WriteLine("Sample,Total Weight (kg)");
                    
                    // Snapshot data for thread safety
                    var data = new List<double>(_totalWeightValues);
                    
                    for (int i = 0; i < data.Count; i++)
                    {
                        writer.WriteLine($"{i},{data[i]:F2}");
                    }
                });

                _dialogService.ShowMessage("Data exported successfully!", "Export Complete");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"File error: {ex.Message}", "Export Error");
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"Access denied: {ex.Message}", "Export Error");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Unexpected export error: {ex.Message}", "Export Error");
            }
        }

        public void Refresh()
        {
            if (_weightProcessor == null) return;

            var latest = _weightProcessor.LatestTotal;
            double rawWeight = latest.TaredWeight;

            // Check calibration status
            // Determine active ADC mode from canService or common state
            // For now, checking both internal and ADS1115 (whichever is active in WeightProcessor)
            var internalCal = _weightProcessor.InternalCalibration;
            var adsCal = _weightProcessor.Ads1115Calibration;
            
            // TODO: Expose a single IsActiveCalibrated property in WeightProcessor to simplify this check
            IsCalibrated = (internalCal?.IsValid == true) || (adsCal?.IsValid == true);

            // Sync UnitLabel with settings
            if (IsBrakeMode)
            {
                string settingUnit = _settings.Settings.BrakeDisplayUnit;
                if (UnitLabel != settingUnit)
                {
                    UnitLabel = settingUnit;
                    if (YAxes.Length > 0)
                    {
                        YAxes[0].Name = UnitLabel == "N" ? "Brake Force (N)" : "Brake Force (kg)";
                    }
                }
            }
            else if (UnitLabel != "kg")
            {
                UnitLabel = "kg";
                if (YAxes.Length > 0)
                {
                    YAxes[0].Name = "Weight (kg)";
                }
            }

            // Apply conversion if in Brake Mode
            if (IsBrakeMode)
            {
                if (_settings.Settings.BrakeDisplayUnit == "N")
                {
                    CurrentWeight = rawWeight * _settings.Settings.BrakeKgToNewtonMultiplier;
                }
                else
                {
                    CurrentWeight = rawWeight; // Default kg
                }
            }
            else
            {
                CurrentWeight = rawWeight;
            }

            // Rate calculation
            _dataPointsCurrentSec++;
            var now = DateTime.Now;
            if ((now - _lastRateCheck).TotalSeconds >= 1.0)
            {
                DataPointsPerSec = _dataPointsCurrentSec;
                _dataPointsCurrentSec = 0;
                _lastRateCheck = now;
                OnPropertyChanged(nameof(DataRateText));
                OnPropertyChanged(nameof(DataPointsPerSec));
            }
            if (CurrentState == TestState.Running)
            {
                // Update Graph
                _totalWeightValues.Add(CurrentWeight);
                if (_totalWeightValues.Count > MaxGraphSamples)
                {
                    _totalWeightValues.RemoveAt(0);
                    XAxes[0].MinLimit = _totalWeightValues.Count - MaxGraphSamples;
                    XAxes[0].MaxLimit = _totalWeightValues.Count;
                }

                // Peak Hold
                if (Math.Abs(CurrentWeight) > 0.1) // 100g or 0.1N noise threshold
                {
                    if (MaxWeight == 0 || CurrentWeight > MaxWeight) MaxWeight = CurrentWeight;
                    if (MinWeight == 0 || CurrentWeight < MinWeight) MinWeight = CurrentWeight;
                    SampleCount++;
                }
            }

            // Stability check for Validation Indicator
            _stabilityBuffer.Enqueue(CurrentWeight);
            if (_stabilityBuffer.Count > StabilityBufferSize) _stabilityBuffer.TryDequeue(out _);

            bool isStable = false;
            if (_stabilityBuffer.Count == StabilityBufferSize)
            {
                double sum = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                foreach (var val in _stabilityBuffer)
                {
                    sum += val;
                    if (val < min) min = val;
                    if (val > max) max = val;
                }
                
                double range = max - min;
                
                double threshold;
                if (IsBrakeMode)
                {
                    threshold = _settings.Settings.BrakeDisplayUnit == "kg" ? 0.5 : 5.0; // 0.5kg or 5N
                }
                else
                {
                    threshold = 0.5; // 0.5kg
                }
                
                isStable = range < threshold;
            }

            // Validation Indicator Logic
            // Green if stable and weight > threshold
            double activeThreshold;
            if (IsBrakeMode)
            {
                activeThreshold = _settings.Settings.BrakeDisplayUnit == "kg" ? 1.0 : 10.0; // 1kg or 10N
            }
            else
            {
                activeThreshold = 1.0; // 1kg
            }

            if (CurrentWeight > activeThreshold && isStable)
                ValidationColor = "#FF28A745"; // Green
            else if (CurrentWeight > activeThreshold)
                ValidationColor = "#FFFFC107"; // Amber (Active but unstable)
            else
                ValidationColor = "#FFDC3545"; // Red (Idle or noise)

            // Notify UI of formatted strings
            OnPropertyChanged(nameof(MainWeightText));
            OnPropertyChanged(nameof(TotalWeightText));
            OnPropertyChanged(nameof(MinWeightText));
            OnPropertyChanged(nameof(MaxWeightText));
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ConnectionColor));
        }

        private void StartTest()
        {
            CurrentState = TestState.Running;
            MaxWeight = 0;
            MinWeight = 0;
            SampleCount = 0;
            _totalWeightValues.Clear();
            _dataLogger.StartLogging();
            
            _accumulatedTime = TimeSpan.Zero;
            _lastTimerTick = DateTime.Now;
            
            if (_testTimer == null)
            {
                _testTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                _testTimer.Tick += (s, e) => 
                {
                    if (CurrentState == TestState.Running)
                    {
                        var now = DateTime.Now;
                        _accumulatedTime += (now - _lastTimerTick);
                        _lastTimerTick = now;
                        TimerText = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)_accumulatedTime.TotalHours, _accumulatedTime.Minutes, _accumulatedTime.Seconds);
                    }
                };
            }
            _testTimer.Start();
        }

        private void StopTest()
        {
            CurrentState = TestState.Idle;
            _dataLogger.StopLogging();
            _testTimer?.Stop();
        }

        private void PauseTest()
        {
            CurrentState = TestState.Paused;
            // Stop the timer to prevent drift (though Tick handler checks state, this is cleaner)
            // But we need to update _lastTimerTick when resuming.
        }

        private void SaveTest()
        {
            // Implementation leveraging the new structure later
             _dialogService.ShowMessage("Save logic will be implemented here.", "Save");
        }

        private void ClearData()
        {
            _totalWeightValues.Clear();
            MaxWeight = 0;
            MinWeight = 0;
            SampleCount = 0;
        }
    }
}
