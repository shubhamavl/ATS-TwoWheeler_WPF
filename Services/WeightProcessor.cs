using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    /// <summary>
    /// Filter type enumeration
    /// </summary>
    public enum FilterType
    {
        None,
        EMA,
        SMA
    }

    /// <summary>
    /// High-performance weight data processor for ATS Two-Wheeler
    /// Runs on dedicated thread to handle 1kHz data rate
    /// Processes single total weight (all 4 channels summed)
    /// </summary>
    public class WeightProcessor : IDisposable
    {
        // Input queue: Raw ADC data from CAN thread
        private readonly ConcurrentQueue<RawWeightData> _rawDataQueue = new();
        
        // Output: Latest processed data (lock-free)
        private volatile ProcessedWeightData _latestTotal = new();
        
        // Calibration references (immutable after set)
        private LinearCalibration? _totalCalibration;
        private TareManager? _tareManager;
        
        // ADC mode tracking (0=Internal, 1=ADS1115)
        private byte _totalADCMode = 0;
        
        // Thread control
        private Task? _processingTask;
        private CancellationTokenSource? _cancellationSource;
        private volatile bool _isRunning = false;
        
        // Performance tracking
        private long _processedCount = 0;
        private long _droppedCount = 0;
        
        // ===== WEIGHT FILTERING (Configurable) =====
        // Filter configuration
        private FilterType _filterType = FilterType.EMA;
        private double _filterAlpha = 0.15;  // EMA alpha (0.0-1.0)
        private int _filterWindowSize = 10;  // SMA window size
        private bool _filterEnabled = true;  // Enable/disable filtering
        
        // EMA filtered weight values
        private double _totalFilteredCalibrated = 0;
        private double _totalFilteredTared = 0;
        
        // Track if EMA filter is initialized (first sample)
        private bool _totalFilterInitialized = false;
        
        // SMA buffers
        private readonly Queue<double> _totalSmaCalibrated = new Queue<double>();
        private readonly Queue<double> _totalSmaTared = new Queue<double>();
        
        public ProcessedWeightData LatestTotal => _latestTotal;
        public long ProcessedCount => _processedCount;
        public long DroppedCount => _droppedCount;
        public bool IsRunning => _isRunning;
        
        /// <summary>
        /// Start the processing thread
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _cancellationSource = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoop(_cancellationSource.Token));
            
            ProductionLogger.Instance.LogInfo("WeightProcessor started", "WeightProcessor");
        }
        
        /// <summary>
        /// Stop the processing thread
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _cancellationSource?.Cancel();
            _processingTask?.Wait(1000);
            
            ProductionLogger.Instance.LogInfo($"WeightProcessor stopped. Processed: {_processedCount}, Dropped: {_droppedCount}", "WeightProcessor");
        }
        
        /// <summary>
        /// Set calibration reference
        /// </summary>
        public void SetCalibration(LinearCalibration? calibration)
        {
            _totalCalibration = calibration;
            
            ProductionLogger.Instance.LogInfo($"Calibration set - Total: {calibration?.IsValid}", "WeightProcessor");
        }
        
        /// <summary>
        /// Set ADC mode (0=Internal, 1=ADS1115)
        /// </summary>
        public void SetADCMode(byte adcMode)
        {
            _totalADCMode = adcMode;
        }
        
        /// <summary>
        /// Set tare manager reference
        /// </summary>
        public void SetTareManager(TareManager tareManager)
        {
            _tareManager = tareManager;
            ProductionLogger.Instance.LogInfo("TareManager set", "WeightProcessor");
        }
        
        /// <summary>
        /// Configure filter settings
        /// </summary>
        public void ConfigureFilter(FilterType type, double alpha, int windowSize, bool enabled)
        {
            _filterType = type;
            _filterAlpha = alpha;
            _filterWindowSize = windowSize;
            _filterEnabled = enabled;
            
            // Clear SMA buffers when settings change
            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();
            
            // Reset EMA filter when changing filter type
            if (type != FilterType.EMA)
            {
                _totalFilterInitialized = false;
            }
            
            ProductionLogger.Instance.LogInfo($"Filter configured: Type={type}, Alpha={alpha}, Window={windowSize}, Enabled={enabled}", "WeightProcessor");
        }
        
        /// <summary>
        /// Enqueue raw ADC data for processing
        /// Supports both Internal ADC (unsigned 0-16380 for 4 channels) and ADS1115 (signed -131072 to +131068)
        /// </summary>
        public void EnqueueRawData(int rawADC)
        {
            const int MAX_QUEUE_SIZE = 100; // Prevent memory leak
            
            if (_rawDataQueue.Count > MAX_QUEUE_SIZE)
            {
                Interlocked.Increment(ref _droppedCount);
                return; // Drop oldest data
            }
            
            _rawDataQueue.Enqueue(new RawWeightData 
            { 
                Side = 0,  // Always 0 for total (kept for compatibility)
                RawADC = rawADC,  // Can be signed (ADS1115) or unsigned (Internal)
                Timestamp = DateTime.Now 
            });
        }
        
        /// <summary>
        /// Processing thread - runs continuously
        /// </summary>
        private void ProcessingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_rawDataQueue.TryDequeue(out var rawData))
                {
                    ProcessRawData(rawData);
                    Interlocked.Increment(ref _processedCount);
                }
                else
                {
                    // No data available - sleep briefly
                    Thread.Sleep(1);
                }
            }
        }
        
        /// <summary>
        /// Core processing - optimized for speed with configurable filtering
        /// </summary>
        private void ProcessRawData(RawWeightData raw)
        {
            var processed = new ProcessedWeightData
            {
                RawADC = raw.RawADC,
                Timestamp = raw.Timestamp
            };
            
            // Apply calibration (fast floating-point math)
            if (_totalCalibration?.IsValid == true)
            {
                double calibratedWeight = _totalCalibration.RawToKg(raw.RawADC);
                
                // Apply filtering if enabled
                if (_filterEnabled)
                {
                    processed.CalibratedWeight = ApplyFilter(calibratedWeight, true);
                }
                else
                {
                    processed.CalibratedWeight = calibratedWeight;
                }
                
                // Apply tare (mode-specific) - uses _totalADCMode which should match the calibration mode
                double taredWeight = _tareManager?.ApplyTare(processed.CalibratedWeight, _totalADCMode) ?? processed.CalibratedWeight;
                
                // Apply filtering to tared weight if enabled
                if (_filterEnabled)
                {
                    processed.TaredWeight = ApplyFilter(taredWeight, false);
                }
                else
                {
                    processed.TaredWeight = taredWeight;
                }
            }
            
            _latestTotal = processed; // Atomic write
        }
        
        /// <summary>
        /// Apply filter based on configured filter type
        /// </summary>
        private double ApplyFilter(double value, bool isCalibrated)
        {
            switch (_filterType)
            {
                case FilterType.EMA:
                    return ApplyEMA(value, isCalibrated);
                case FilterType.SMA:
                    return ApplySMA(value, isCalibrated);
                case FilterType.None:
                default:
                    return value;
            }
        }
        
        /// <summary>
        /// Apply Exponential Moving Average filter
        /// </summary>
        private double ApplyEMA(double value, bool isCalibrated)
        {
            if (isCalibrated)
            {
                if (!_totalFilterInitialized)
                {
                    _totalFilteredCalibrated = value;
                    _totalFilterInitialized = true;
                    return value;
                }
                _totalFilteredCalibrated = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredCalibrated;
                return _totalFilteredCalibrated;
            }
            else
            {
                if (!_totalFilterInitialized)
                {
                    _totalFilteredTared = value;
                    return value;
                }
                _totalFilteredTared = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredTared;
                return _totalFilteredTared;
            }
        }
        
        /// <summary>
        /// Apply Simple Moving Average filter
        /// </summary>
        private double ApplySMA(double value, bool isCalibrated)
        {
            Queue<double> buffer = isCalibrated ? _totalSmaCalibrated : _totalSmaTared;
            
            buffer.Enqueue(value);
            if (buffer.Count > _filterWindowSize)
            {
                buffer.Dequeue();
            }
            
            // Return average of buffer
            return buffer.Count > 0 ? buffer.Average() : value;
        }
        
        /// <summary>
        /// Reset filters (call when tare changes or calibration changes)
        /// </summary>
        public void ResetFilters()
        {
            _totalFilterInitialized = false;
            _totalFilteredCalibrated = 0;
            _totalFilteredTared = 0;
            
            // Clear SMA buffers
            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();
            
            ProductionLogger.Instance.LogInfo("Weight filters reset", "WeightProcessor");
        }
        
        public void Dispose()
        {
            Stop();
            _cancellationSource?.Dispose();
        }
    }
}

