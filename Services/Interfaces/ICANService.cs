using System;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Adapters;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface ICANService : IDisposable
    {
        bool IsConnected { get; }
        bool IsStreaming { get; }
        byte CurrentADCMode { get; }
        long TxMessageCount { get; }
        long RxMessageCount { get; }
        event Action<CANMessage>? MessageReceived;
        event EventHandler<RawDataEventArgs>? RawDataReceived;
        event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        event EventHandler<string>? DataTimeout;

        bool Connect(CanAdapterConfig config, out string errorMessage);
        void Disconnect();
        bool SendMessage(uint id, byte[] data);
        bool StartStream(byte rate);
        bool StopAllStreams();
        bool SwitchToInternalADC();
        bool SwitchToADS1115();
        bool SwitchSystemMode(bool isBrakeMode);
        bool RequestSystemStatus();
        bool RequestFirmwareVersion();
        void SetTimeout(TimeSpan timeout);
    }
}
