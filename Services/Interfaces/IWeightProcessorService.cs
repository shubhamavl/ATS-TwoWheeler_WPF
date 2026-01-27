using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IWeightProcessorService : IDisposable
    {
        ProcessedWeightData LatestTotal { get; }
        LinearCalibration? InternalCalibration { get; }
        LinearCalibration? Ads1115Calibration { get; }
        
        void Start();
        void Stop();
        void SetCalibration(LinearCalibration? calibration, AdcMode mode = AdcMode.InternalWeight);
        void SetADCMode(AdcMode mode);
        void SetBrakeMode(bool isBrakeMode);
        void LoadCalibration();
        void SetTareManager(TareManager tareManager);
        void ConfigureFilter(FilterType type, double alpha, int windowSize, bool enabled);
        void EnqueueRawData(int rawValue);
        void ResetFilters();
        void Tare();
        void ResetTare();
    }
}
