using System;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IStatusMonitorService : IDisposable
    {
        void StartMonitoring();
        void StopMonitoring();
        bool IsSystemAvailable { get; }
        event EventHandler<bool> AvailabilityChanged;
    }
}
