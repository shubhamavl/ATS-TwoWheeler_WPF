using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Adapters;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class CalibrationDialogViewModel : BaseViewModel
    {
        private readonly ICANService _canService;
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialogService;
        private readonly IProductionLoggerService _logger;
        private readonly IWeightProcessorService _weightProcessor;
        private LinearCalibration? _internalCalResult;
        private LinearCalibration? _ads1115CalResult;

        private byte _adcMode;
        private bool _isBrakeMode;
        private int _calibrationDelayMs;
        private bool _isCapturingDualMode;
        private int _currentRawADC;

        public event EventHandler? RequestClose;
        public event EventHandler<CalibrationDialogResultsEventArgs>? CalculationCompleted;

        public ObservableCollection<CalibrationPointViewModel> Points { get; } = new();

        private int _capturedPointCount;
        public int CapturedPointCount
        {
            get => _capturedPointCount;
            set => SetProperty(ref _capturedPointCount, value);
        }

        private bool _isNewtonCalibration;
        public bool IsNewtonCalibration
        {
            get => _isNewtonCalibration;
            set
            {
                if (SetProperty(ref _isNewtonCalibration, value))
                {
                    OnPropertyChanged(nameof(InputUnitHeader));
                }
            }
        }

        public string InputUnitLabel => "Input Weight (kg):";
        public string TargetUnit => "Kilograms (kg)";
        public string InputUnitHeader => "Input (kg) → Target (kg)";

        public string DialogTitle => _isBrakeMode ? "Brake Force Calibration" : "Total Weight Calibration";
        public string DialogSubtitle => _isBrakeMode ? "Brake Force Measurement System" : "Hardware Calibration Process";

        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
        public ICommand CapturePointCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand EditPointCommand { get; }
        public ICommand SavePointCommand { get; }
        public ICommand CancelPointCommand { get; }

        public CalibrationDialogViewModel(ICANService canService, ISettingsService settings, IDialogService dialogService, IProductionLoggerService logger, IWeightProcessorService weightProcessor, byte adcMode = 0, int calibrationDelayMs = 500, bool isBrakeMode = false)
        {
            _canService = canService;
            _settings = settings;
            _dialogService = dialogService;
            _logger = logger;
            _weightProcessor = weightProcessor;
            _adcMode = adcMode;
            _calibrationDelayMs = calibrationDelayMs;
            _isBrakeMode = isBrakeMode;

            IsNewtonCalibration = _isBrakeMode;

            AddPointCommand = new RelayCommand(_ => AddNewPoint());
            RemovePointCommand = new RelayCommand(p => RemovePoint(p as CalibrationPointViewModel));
            CapturePointCommand = new RelayCommand(async p => await OnCapturePoint(p as CalibrationPointViewModel));
            CalculateCommand = new RelayCommand(_ => OnCalculate(), _ => CapturedPointCount >= 1);
            SaveCommand = new RelayCommand(_ => OnSave());
            EditPointCommand = new RelayCommand(p => OnEditPoint(p as CalibrationPointViewModel));
            SavePointCommand = new RelayCommand(p => OnSavePoint(p as CalibrationPointViewModel));
            CancelPointCommand = new RelayCommand(p => OnCancelPoint(p as CalibrationPointViewModel));

            if (_canService.IsConnected)
            {
                _canService.RawDataReceived += OnRawDataReceived;
            }

            AddNewPoint();
        }

        private void OnRawDataReceived(object? sender, RawDataEventArgs e)
        {
            _currentRawADC = e.RawADCSum;

            // Update live ADC for points not yet captured
            foreach (var point in Points.Where(p => !p.BothModesCaptured))
            {
                if (_adcMode == 0)
                {
                    if (_currentRawADC >= 0 && _currentRawADC <= 16380)
                    {
                        point.InternalADC = (ushort)_currentRawADC;
                    }
                }
                else
                {
                    point.ADS1115ADC = _currentRawADC;
                }
            }
        }

        private void AddNewPoint()
        {
            Points.Add(new CalibrationPointViewModel
            {
                PointNumber = Points.Count + 1,
                KnownWeight = 0
            });
            UpdatePointNumbers();
        }

        private void RemovePoint(CalibrationPointViewModel? point)
        {
            if (point == null)
            {
                return;
            }

            if (Points.Count <= 1)
            {
                _dialogService.ShowMessage("At least one calibration point is required.", "Cannot Remove");
                return;
            }

            if (_dialogService.ShowConfirmation($"Remove Point {point.PointNumber}?", "Confirm Removal"))
            {
                Points.Remove(point);
                UpdatePointNumbers();
                UpdateCapturedCount();
            }
        }

        private void UpdatePointNumbers()
        {
            for (int i = 0; i < Points.Count; i++)
            {
                Points[i].PointNumber = i + 1;
            }
        }

        private void UpdateCapturedCount()
        {
            CapturedPointCount = Points.Count(p => p.IsCaptured);
        }

        private async Task OnCapturePoint(CalibrationPointViewModel? point)
        {
            if (point == null || _isCapturingDualMode)
            {
                return;
            }

            if (point.KnownWeight < 0 || point.KnownWeight > 10000)
            {
                _dialogService.ShowMessage("Invalid weight input.", "Validation Error");
                return;
            }

            _isCapturingDualMode = true;
            try
            {
                if (_canService.IsConnected)
                {
                    // Call the logic similar to CaptureDualModeWithStream
                    // Note: This logic might need further refactoring into a Service later
                    await CaptureDualMode(point);
                }
                else
                {
                    // Manual capture - for simplicity in this refactor, we omitted manual dialog here
                    _dialogService.ShowMessage("CAN Service not connected. Manual entry not yet implemented in ViewModel.", "Manual Entry");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Capture failed: {ex.Message}", "Error");
            }
            finally
            {
                _isCapturingDualMode = false;
                UpdateCapturedCount();
                if (point == Points.Last() && point.IsCaptured)
                {
                    AddNewPoint();
                }
            }
        }

        private async Task CaptureDualMode(CalibrationPointViewModel point)
        {
            // Set weight for the point prior to capture
            double currentWeight = point.KnownWeight;
            // Always kg during calibration target as per user clarification

            // Statistics collection parameters from settings
            var s = _settings.Settings;
            int samples = s.CalibrationSampleCount;
            int duration = s.CalibrationCaptureDurationMs;
            bool useMedian = s.CalibrationUseMedian;
            bool removeOutliers = s.CalibrationRemoveOutliers;
            double threshold = s.CalibrationOutlierThreshold;
            double maxStdDev = s.CalibrationMaxStdDev;

            // Apply start delay if configured
            if (_calibrationDelayMs > 0)
            {
                point.StatusText = $"Waiting {_calibrationDelayMs}ms...";
                await Task.Delay(_calibrationDelayMs);
            }

            // Step 1: Capture current mode
            point.StatusText = $"Capturing Mode {(_adcMode == 0 ? "Internal" : "ADS1115")}...";
            var result1 = await CalibrationStatistics.CaptureAveragedADC(
                samples, 
                duration, 
                () => _currentRawADC,
                null, 
                useMedian, 
                removeOutliers, 
                threshold, 
                maxStdDev);

            if (_adcMode == 0)
            {
                point.InternalADC = (ushort)result1.AveragedValue;
            }
            else
            {
                point.ADS1115ADC = result1.AveragedValue;
            }

            // Step 2: Switch mode
            point.StatusText = "Switching ADC Mode...";
            if (_adcMode == 0) { _canService.SwitchToADS1115(); _adcMode = 1; }
            else { _canService.SwitchToInternalADC(); _adcMode = 0; }

            await Task.Delay(1000); // Wait for switch and data stabilization

            // Step 3: Capture second mode
            point.StatusText = $"Capturing Mode {(_adcMode == 0 ? "Internal" : "ADS1115")}...";
            var result2 = await CalibrationStatistics.CaptureAveragedADC(
                samples, 
                duration, 
                () => _currentRawADC,
                null, 
                useMedian, 
                removeOutliers, 
                threshold, 
                maxStdDev);

            if (_adcMode == 0)
            {
                point.InternalADC = (ushort)result2.AveragedValue;
            }
            else
            {
                point.ADS1115ADC = result2.AveragedValue;
            }

            point.CaptureSampleCount = samples;
            point.CaptureMean = result2.Mean;
            point.CaptureStdDev = result2.StandardDeviation;
            point.CaptureStabilityWarning = result2.IsStable ? "" : "UNSTABLE READING";

            point.BothModesCaptured = true;
            point.IsCaptured = true;
        }

        private void OnCalculate()
        {
            try
            {
                // Factor is always 1.0 because calibration target is always KG as per user clarification
                double factor = 1.0;
                var internalPoints = Points.Select(p =>
                {
                    var cp = p.ToCalibrationPointInternal();
                    cp.KnownWeight *= factor;
                    return cp;
                }).ToList();

                var adsPoints = Points.Select(p =>
                {
                    var cp = p.ToCalibrationPointADS1115();
                    cp.KnownWeight *= factor;
                    return cp;
                }).ToList();

                _internalCalResult = LinearCalibration.FitMultiplePoints(internalPoints);
                _ads1115CalResult = LinearCalibration.FitMultiplePoints(adsPoints);

                _internalCalResult.ADCMode = AdcMode.InternalWeight;
                _internalCalResult.SystemMode = _isBrakeMode ? SystemMode.Brake : SystemMode.Weight;

                _ads1115CalResult.ADCMode = AdcMode.Ads1115;
                _ads1115CalResult.SystemMode = _isBrakeMode ? SystemMode.Brake : SystemMode.Weight;

                string resultsMsg = $"Calculation Successful!\n\n" +
                                   $"Internal: {_internalCalResult.GetEquationString()} (R²={_internalCalResult.R2:F4})\n" +
                                   $"ADS1115: {_ads1115CalResult.GetEquationString()} (R²={_ads1115CalResult.R2:F4})";

                _dialogService.ShowMessage(resultsMsg, "Calibration Calculated");

                CalculationCompleted?.Invoke(this, new CalibrationDialogResultsEventArgs
                {
                    InternalEquation = _internalCalResult.GetEquationString(),
                    AdsEquation = _ads1115CalResult.GetEquationString(),
                    IsSuccessful = true
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Calculation failed: {ex.Message}", "Error");
            }
        }

        private void OnSave()
        {
            if (_internalCalResult == null || _ads1115CalResult == null)
            {
                _dialogService.ShowMessage("Please calculate before saving.", "Warning");
                return;
            }

            try
            {
                _internalCalResult.SaveToFile();
                _ads1115CalResult.SaveToFile();

                _weightProcessor.LoadCalibration();
                _dialogService.ShowMessage("Calibration saved and applied to system.", "Success");
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Failed to save calibration: {ex.Message}", "Error");
            }
        }

        private void OnEditPoint(CalibrationPointViewModel? point)
        {
            if (point != null)
            {
                point.IsEditing = true;
            }
        }

        private void OnSavePoint(CalibrationPointViewModel? point)
        {
            if (point == null)
            {
                return;
            }
            // Add validation here
            point.IsEditing = false;
            UpdateCapturedCount();
        }

        private void OnCancelPoint(CalibrationPointViewModel? point)
        {
            if (point != null)
            {
                point.IsEditing = false;
            }
        }

        public override void Dispose()
        {
            if (_canService != null)
            {
                _canService.RawDataReceived -= OnRawDataReceived;
            }
            base.Dispose();
        }
    }
}
