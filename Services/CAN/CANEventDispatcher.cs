using System;
using ATS_TwoWheeler_WPF.Models;

namespace ATS_TwoWheeler_WPF.Services.CAN
{
    /// <summary>
    /// Dispatches CAN events to subscribers
    /// </summary>
    public class CANEventDispatcher
    {
        // v0.1 Protocol Events
        public event EventHandler<RawDataEventArgs>? RawDataReceived;
        public event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        public event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        public event EventHandler<string>? DataTimeout;

        private byte _currentADCMode = 0; // Track current ADC mode (0=Internal, 1=ADS1115)

        public byte CurrentADCMode
        {
            get => _currentADCMode;
            set => _currentADCMode = value;
        }

        /// <summary>
        /// Fire timeout event
        /// </summary>
        public void FireTimeout(string timeoutMessage)
        {
            DataTimeout?.Invoke(this, timeoutMessage);
        }

        /// <summary>
        /// Fire specific events based on CAN ID and data
        /// </summary>
        public void FireSpecificEvents(uint canId, byte[] canData)
        {
            switch (canId)
            {
                case CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA: // 0x200
                    HandleRawData(canData);
                    break;

                case CANMessageProcessor.CAN_MSG_ID_SYSTEM_STATUS: // 0x300
                    HandleSystemStatus(canData);
                    break;

                case CANMessageProcessor.CAN_MSG_ID_SYS_PERF: // 0x302
                    HandlePerformanceMetrics(canData);
                    break;

                case CANMessageProcessor.CAN_MSG_ID_VERSION_RESPONSE: // 0x301
                    HandleVersionResponse(canData);
                    break;
            }
        }

        private void HandleRawData(byte[] canData)
        {
            if (_currentADCMode == 0) // Internal ADC
            {
                if (canData.Length >= 2)
                {
                    ushort rawADC = (ushort)(canData[0] | (canData[1] << 8));
                    RawDataReceived?.Invoke(this, new RawDataEventArgs
                    {
                        RawADCSum = rawADC,
                        TimestampFull = DateTime.Now
                    });
                }
            }
            else // ADS1115
            {
                if (canData.Length >= 4)
                {
                    int rawADC = BitConverter.ToInt32(canData, 0);
                    RawDataReceived?.Invoke(this, new RawDataEventArgs
                    {
                        RawADCSum = rawADC,
                        TimestampFull = DateTime.Now
                    });
                }
            }
        }

        private void HandleSystemStatus(byte[] canData)
        {
            if (canData != null && canData.Length >= 3)
            {
                byte packed = canData[0];
                byte systemStatus = (byte)(packed & 0x03);
                byte adcMode = (byte)((packed >> 2) & 0x01);
                byte relayState = (byte)((packed >> 3) & 0x01);
                byte errorFlags = canData[1];

                uint uptime = 0;
                if (canData.Length >= 6)
                {
                    uptime = BitConverter.ToUInt32(canData, 2);
                }

                _currentADCMode = adcMode;

                SystemStatusReceived?.Invoke(this, new SystemStatusEventArgs
                {
                    SystemStatus = systemStatus,
                    ErrorFlags = errorFlags,
                    ADCMode = adcMode,
                    RelayState = relayState,
                    UptimeSeconds = uptime,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void HandlePerformanceMetrics(byte[] canData)
        {
            if (canData != null && canData.Length >= 4)
            {
                ushort canHz = BitConverter.ToUInt16(canData, 0);
                ushort adcHz = BitConverter.ToUInt16(canData, 2);

                PerformanceMetricsReceived?.Invoke(this, new PerformanceMetricsEventArgs
                {
                    CanTxHz = canHz,
                    AdcSampleHz = adcHz,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void HandleVersionResponse(byte[] canData)
        {
            if (canData != null && canData.Length >= 4)
            {
                FirmwareVersionReceived?.Invoke(this, new FirmwareVersionEventArgs
                {
                    Major = canData[0],
                    Minor = canData[1],
                    Patch = canData[2],
                    Build = canData[3],
                    Timestamp = DateTime.Now
                });
            }
        }
    }
}
