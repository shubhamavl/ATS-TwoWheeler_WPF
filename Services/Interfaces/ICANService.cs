using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Adapters;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface ICANService : IDisposable
    {
        bool IsConnected { get; }
        bool IsStreaming { get; }
        AdcMode CurrentADCMode { get; }
        long TxMessageCount { get; }
        long RxMessageCount { get; }
        DateTime LastRxTime { get; }
        DateTime LastSystemStatusTime { get; }
        event Action<CANMessage>? MessageReceived;
        event EventHandler<RawDataEventArgs>? RawDataReceived;
        event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        event EventHandler<string>? DataTimeout;

        bool Connect(CanAdapterConfig config, out string errorMessage);
        void Disconnect();
        bool SendMessage(uint id, byte[] data);
        bool StartStream(TransmissionRate rate);
        bool StopAllStreams();
        bool SwitchToInternalADC();
        bool SwitchToADS1115();
        bool SwitchSystemMode(SystemMode mode);
        bool RequestSystemStatus();
        bool RequestFirmwareVersion();
        void SetTimeout(TimeSpan timeout);
    }
}
