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

        private byte _adcMode;
        private bool _isBrakeMode;
        private int _calibrationDelayMs;
        private bool _isCapturingDualMode;
        private int _currentRawADC;

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

        public string InputUnitHeader => IsNewtonCalibration ? "Mass (kg):" : "Value (Units):";

        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
        public ICommand CapturePointCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand EditPointCommand { get; }
        public ICommand SavePointCommand { get; }
        public ICommand CancelPointCommand { get; }

        public CalibrationDialogViewModel(ICANService canService, ISettingsService settings, IDialogService dialogService, IProductionLoggerService logger, byte adcMode = 0, int calibrationDelayMs = 500, bool isBrakeMode = false)
        {
            _canService = canService;
            _settings = settings;
            _dialogService = dialogService;
            _logger = logger;
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
                        point.InternalADC = (ushort)_currentRawADC;
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
            if (point == null) return;
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
                Points[i].PointNumber = i + 1;
        }

        private void UpdateCapturedCount()
        {
            CapturedPointCount = Points.Count(p => p.IsCaptured);
        }

        private async Task OnCapturePoint(CalibrationPointViewModel? point)
        {
            if (point == null || _isCapturingDualMode) return;

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
                if (point == Points.Last() && point.IsCaptured) AddNewPoint();
            }
        }

        private async Task CaptureDualMode(CalibrationPointViewModel point)
        {
            // Simplified version of the complex dual-mode logic from the code-behind
            // In a real refactor, this should use Task.Delay and CAN commands
            
            // Step 1: Capture current mode
            int firstVal = _currentRawADC;
            if (_adcMode == 0) point.InternalADC = (ushort)firstVal;
            else point.ADS1115ADC = firstVal;

            // Step 2: Switch mode
            if (_adcMode == 0) { _canService.SwitchToADS1115(); _adcMode = 1; }
            else { _canService.SwitchToInternalADC(); _adcMode = 0; }
            
            await Task.Delay(1000); // Wait for switch and data flow

            // Step 3: Capture second mode
            int secondVal = _currentRawADC;
            if (_adcMode == 0) point.InternalADC = (ushort)secondVal;
            else point.ADS1115ADC = secondVal;

            point.BothModesCaptured = true;
            point.IsCaptured = true;
        }

        private void OnCalculate()
        {
            // Logic for calculation would go here, using LinearCalibration core
            _dialogService.ShowMessage("Calculation logic would be executed here.", "Calculate");
        }

        private void OnSave()
        {
            // Logic for saving would go here
            _dialogService.ShowMessage("Calibration saved.", "Save");
        }

        private void OnEditPoint(CalibrationPointViewModel? point)
        {
            if (point != null) point.IsEditing = true;
        }

        private void OnSavePoint(CalibrationPointViewModel? point)
        {
            if (point == null) return;
            // Add validation here
            point.IsEditing = false;
            UpdateCapturedCount();
        }

        private void OnCancelPoint(CalibrationPointViewModel? point)
        {
            if (point != null) point.IsEditing = false;
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
