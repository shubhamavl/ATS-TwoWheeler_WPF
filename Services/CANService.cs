using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Adapters;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Services.CAN;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Core.Exceptions;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("ATS_TwoWheeler_WPF.Tests")]

namespace ATS_TwoWheeler_WPF.Services
{

    // USB-CAN-A Binary Protocol Implementation for ATS Two-Wheeler System
    public class CANService : ICANService
    {
        private const uint MAX_CAN_ID = 0x7FF; // 11-bit standard CAN ID maximum
        
        private SerialPort? _serialPort;
        private readonly ConcurrentQueue<byte> _frameBuffer = new();
        private volatile bool _connected;
        private volatile bool _isStreaming;
        public long TxMessageCount { get; private set; }
        public long RxMessageCount { get; private set; }
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _sendLock = new object();
        private DateTime _lastMessageTime = DateTime.MinValue;
        private TimeSpan _timeout = TimeSpan.FromSeconds(5); // Configurable timeout
        private bool _timeoutNotified = false;
        
        public DateTime LastRxTime => _lastMessageTime;
        public DateTime LastSystemStatusTime { get; private set; } = DateTime.MinValue;

        // v0.1 Ultra-Minimal CAN Protocol - Semantic IDs & Maximum Efficiency
        // Raw Data: 2 bytes only (75% reduction from 8 bytes)
        // Stream Control: 1 byte only (87.5% reduction from 8 bytes)
        // System Status: 3 bytes only (62.5% reduction from 8 bytes)

        // CAN Message IDs - Refactored to CANMessageProcessor
        private const uint CAN_MSG_ID_TOTAL_RAW_DATA = CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA;
        private const uint CAN_MSG_ID_START_STREAM = CANMessageProcessor.CAN_MSG_ID_START_STREAM;
        private const uint CAN_MSG_ID_STOP_ALL_STREAMS = CANMessageProcessor.CAN_MSG_ID_STOP_ALL_STREAMS;
        private const uint CAN_MSG_ID_SYSTEM_STATUS = CANMessageProcessor.CAN_MSG_ID_SYSTEM_STATUS;
        private const uint CAN_MSG_ID_SYS_PERF = CANMessageProcessor.CAN_MSG_ID_SYS_PERF;
        private const uint CAN_MSG_ID_STATUS_REQUEST = CANMessageProcessor.CAN_MSG_ID_STATUS_REQUEST;
        private const uint CAN_MSG_ID_MODE_INTERNAL = CANMessageProcessor.CAN_MSG_ID_MODE_INTERNAL;
        private const uint CAN_MSG_ID_MODE_ADS1115 = CANMessageProcessor.CAN_MSG_ID_MODE_ADS1115;
        private const uint CAN_MSG_ID_VERSION_REQUEST = CANMessageProcessor.CAN_MSG_ID_VERSION_REQUEST;
        private const uint CAN_MSG_ID_SET_SYSTEM_MODE = CANMessageProcessor.CAN_MSG_ID_SET_SYSTEM_MODE;
        private const uint CAN_MSG_ID_VERSION_RESPONSE = CANMessageProcessor.CAN_MSG_ID_VERSION_RESPONSE;

        // Bootloader protocol IDs (matching STM32 implementation)
        private const uint CAN_MSG_ID_BOOT_ENTER = BootloaderProtocol.CanIdBootEnter;
        private const uint CAN_MSG_ID_BOOT_QUERY_INFO = BootloaderProtocol.CanIdBootQueryInfo;
        private const uint CAN_MSG_ID_BOOT_PING = BootloaderProtocol.CanIdBootPing;
        private const uint CAN_MSG_ID_BOOT_BEGIN = BootloaderProtocol.CanIdBootBegin;
        private const uint CAN_MSG_ID_BOOT_END = BootloaderProtocol.CanIdBootEnd;
        private const uint CAN_MSG_ID_BOOT_RESET = BootloaderProtocol.CanIdBootReset;
        private const uint CAN_MSG_ID_BOOT_DATA = BootloaderProtocol.CanIdBootData;
        private const uint CAN_MSG_ID_BOOT_PING_RESPONSE = BootloaderProtocol.CanIdBootPingResponse;
        private const uint CAN_MSG_ID_BOOT_BEGIN_RESPONSE = BootloaderProtocol.CanIdBootBeginResponse;
        private const uint CAN_MSG_ID_BOOT_PROGRESS = BootloaderProtocol.CanIdBootProgress;
        private const uint CAN_MSG_ID_BOOT_END_RESPONSE = BootloaderProtocol.CanIdBootEndResponse;
        private const uint CAN_MSG_ID_BOOT_ERROR = BootloaderProtocol.CanIdBootError;
        private const uint CAN_MSG_ID_BOOT_QUERY_RESPONSE = BootloaderProtocol.CanIdBootQueryResponse;

        // Rate Selection Codes
        private const byte CAN_RATE_100HZ = 0x01;  // 100Hz (10ms interval)
        private const byte CAN_RATE_500HZ = 0x02;  // 500Hz (2ms interval)
        private const byte CAN_RATE_1KHZ = 0x03;   // 1kHz (1ms interval)
        private const byte CAN_RATE_1HZ = 0x05;    // 1Hz (1000ms interval)

        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<string>? DataTimeout;
        
        // v0.1 Events
        public event EventHandler<RawDataEventArgs>? RawDataReceived;
        public event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        public event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        


        public bool IsConnected => _connected;
        public bool IsStreaming 
        { 
            get => _isStreaming;
            private set => _isStreaming = value;
        }
        public AdcMode CurrentADCMode => _eventDispatcher.CurrentADCMode;
        
        private ICanAdapter? _adapter;
        private readonly CANEventDispatcher _eventDispatcher;

        public CANService()
        {
            _connected = false;
            _eventDispatcher = new CANEventDispatcher();
            
            // Wire up event dispatcher to service events
            _eventDispatcher.RawDataReceived += (s, e) => RawDataReceived?.Invoke(this, e);
            _eventDispatcher.SystemStatusReceived += (s, e) => 
            {
                LastSystemStatusTime = DateTime.Now;
                SystemStatusReceived?.Invoke(this, e);
            };
            _eventDispatcher.FirmwareVersionReceived += (s, e) => FirmwareVersionReceived?.Invoke(this, e);
            _eventDispatcher.PerformanceMetricsReceived += (s, e) => PerformanceMetricsReceived?.Invoke(this, e);
            _eventDispatcher.DataTimeout += (s, e) => DataTimeout?.Invoke(this, e);
        }

        /// <summary>
        /// Set the CAN adapter to use
        /// </summary>
        public void SetAdapter(ICanAdapter adapter)
        {
            if (_adapter != null)
            {
                _adapter.MessageReceived -= OnAdapterMessageReceived;
                _adapter.DataTimeout -= OnAdapterDataTimeout;
                _adapter.ConnectionStatusChanged -= OnAdapterConnectionStatusChanged;
                _adapter.Disconnect();
            }

            _adapter = adapter;
            _adapter.MessageReceived += OnAdapterMessageReceived;
            _adapter.DataTimeout += OnAdapterDataTimeout;
            _adapter.ConnectionStatusChanged += OnAdapterConnectionStatusChanged;
        }

        /// <summary>
        /// Connect using adapter configuration
        /// </summary>
        public bool Connect(CanAdapterConfig config, out string errorMessage)
        {
            ICanAdapter? adapter = null;

            if (config is UsbSerialCanAdapterConfig)
            {
                adapter = new UsbSerialCanAdapter();
            }
            else if (config is PcanCanAdapterConfig)
            {
                adapter = new PcanCanAdapter();
            }
            else
            {
                errorMessage = "Unknown adapter configuration type";
                return false;
            }

            SetAdapter(adapter);
            bool result = adapter.Connect(config, out errorMessage);
            _connected = result;
            return result;
        }

        private void OnAdapterMessageReceived(CANMessage message)
        {
            if (message.Direction == "TX") TxMessageCount++;
            else RxMessageCount++;
            
            MessageReceived?.Invoke(message);
            // Fire specific events for protocol messages
            // Note: Some messages (like status/version requests) may have empty data, but responses should have data
            if (message.Data != null)
            {
                _eventDispatcher.FireSpecificEvents(message.ID, message.Data);
            }
        }

        private void OnAdapterDataTimeout(object? sender, string timeoutMessage)
        {
            DataTimeout?.Invoke(this, timeoutMessage);
            _isStreaming = false;
        }

        private void OnAdapterConnectionStatusChanged(object? sender, bool connected)
        {
            _connected = connected;
        }

        public bool Connect(string portName, out string message, int baudRate = 2000000)
        {
            message = string.Empty;
            try
            {
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _serialPort.Open();
                _connected = true;

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ReadMessagesAsync(_cancellationTokenSource.Token));

                ProductionLogger.Instance.LogInfo($"USB-CAN-A Connected on {portName}", "CANService");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "Access denied to COM port. It may be in use by another application.";
                ProductionLogger.Instance.LogError($"CAN connection error: {message} - {ex.Message}", "CANService");
                return false;
            }
            catch (IOException ex)
            {
                message = "IO error while opening COM port.";
                ProductionLogger.Instance.LogError($"CAN connection error: {message} - {ex.Message}", "CANService");
                return false;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ProductionLogger.Instance.LogError($"CAN connection error: {ex.Message}", "CANService");
                throw new CANConnectionException(portName, ex.Message, ex);
            }
        }

        public bool Connect(ushort channel = 0, ushort baudRate = 250)
        {
            try
            {
                // Find available COM ports
                string[] availablePorts = SerialPort.GetPortNames();

                if (availablePorts.Length == 0)
                {
                    System.Windows.MessageBox.Show("No COM ports found!\n\n" +
                                                  "Please check:\n" +
                                                  " USB-CAN-A is connected\n" +
                                                  " CH341 driver is installed\n" +
                                                  " Device appears in Device Manager",
                                                  "COM Port Not Found");
                    return false;
                }

                // Use the last COM port (usually the USB-CAN-A device)
                string selectedPort = availablePorts[availablePorts.Length - 1];
                string message;
                return Connect(selectedPort, out message);
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"USB-CAN-A connection error: {ex.Message}", "CANService");
                throw new CANConnectionException("Auto", ex.Message, ex);
            }
        }

        public void Disconnect()
        {
            _connected = false;
            _isStreaming = false;
            _cancellationTokenSource?.Cancel();
            if (_serialPort?.IsOpen == true)
                _serialPort.Close();

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            ProductionLogger.Instance.LogInfo("USB-CAN-A Disconnected", "CANService");
        }

        private async Task ReadMessagesAsync(CancellationToken token)
        {
            var buffer = new byte[256];
            _lastMessageTime = DateTime.UtcNow;

            while (_connected && !token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort is { IsOpen: true } && _serialPort.BytesToRead > 0)
                    {
                        int count = _serialPort.Read(buffer, 0, buffer.Length);

                        for (int i = 0; i < count; i++)
                            _frameBuffer.Enqueue(buffer[i]);

                        ProcessFrames();

                        // Update last received time
                        _lastMessageTime = DateTime.UtcNow;
                        _timeoutNotified = false;
                    }
                }
                catch
                {
                    // ignore read errors for now
                }

                // Check for timeout
                if (!_timeoutNotified && DateTime.UtcNow - _lastMessageTime > _timeout)
                {
                    _timeoutNotified = true;
                    _eventDispatcher.FireTimeout("Timeout");
                }

                await Task.Delay(5, token);
            }
        }

        private void ProcessFrames()
        {
            while (_frameBuffer.Count >= 20)
            {
                if (!_frameBuffer.TryPeek(out byte first) || first != 0xAA)
                {
                    _frameBuffer.TryDequeue(out _);
                    continue;
                }

                if (_frameBuffer.Count < 20) break;

                var frame = new byte[20];
                for (int i = 0; i < 20; i++)
                    _frameBuffer.TryDequeue(out frame[i]);

                DecodeFrame(frame);
            }
        }

        // PERMANENT FIX: Frame decoding with proper CAN ID extraction (matching working steering code)
        private void DecodeFrame(byte[] frame)
        {
            if (frame.Length < 18 || frame[0] != 0xAA)
                return;

            try
            {
                var (canId, canData) = CANMessageProcessor.DecodeFrame(frame);

                // Process two-wheeler system messages
                if (CANMessageProcessor.IsTwoWheelerMessage(canId))
                {
                    var canMessage = new CANMessage(canId, canData);
                    MessageReceived?.Invoke(canMessage);
                    _eventDispatcher.FireSpecificEvents(canId, canData);
                }
            }
            catch (ArgumentException ex)
            {
                ProductionLogger.Instance.LogError($"Frame data error: {ex.Message}", "CANService");
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Decode error: {ex.Message}", "CANService");
                throw new CANException($"Failed to decode CAN frame: {ex.Message}", ex);
            }
        }



        public bool SendMessage(uint id, byte[] data, bool log = true)
        {
            if (!_connected || _adapter == null) return false;

            try
            {
                // Validate CAN ID (11-bit max for standard frame)
                if (id > MAX_CAN_ID)
                {
                    ProductionLogger.Instance.LogWarning($"Invalid CAN ID: 0x{id:X3} (max 0x{MAX_CAN_ID:X3} for standard frame)", "CANService");
                    return false;
                }

                // Validate data length
                if (data != null && data.Length > 8)
                {
                    ProductionLogger.Instance.LogWarning($"Invalid data length: {data.Length} (max 8 bytes)", "CANService");
                    return false;
                }

                if (data == null)
                {
                    ProductionLogger.Instance.LogWarning("Cannot send message: data is null", "CANService");
                    return false;
                }

                bool result = _adapter.SendMessage(id, data);
                
                // Fire event for TX messages - REMOVED to avoid duplication as adapter fires it
                // var txMessage = new CANMessage(id, data ?? new byte[0], DateTime.Now, "TX");
                // TxMessageCount++; 
                // MessageReceived?.Invoke(txMessage);

                if (log)
                {
                    string dataStr = (data == null || data.Length == 0) ? "[No Data]" : BitConverter.ToString(data);
                    ProductionLogger.Instance.LogInfo($"CAN: Sent CAN frame ID=0x{id:X3}, Data={dataStr}", "CANService");
                }
                return result;
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Send message error: {ex.Message}", "CANService");
                throw new CANSendException(id, ex.Message);
            }
        }

        // Create frame using the working steering code method
        private static byte[] CreateFrame(uint id, byte[] data)
        {
            var frame = new List<byte>
            {
                0xAA,
                (byte)(0xC0 | Math.Min(data?.Length ?? 0, 8)),
                (byte)(id & 0xFF),
                (byte)((id >> 8) & 0xFF)
            };

            frame.AddRange((data ?? new byte[0]).Take(8));
            while (frame.Count < 12)
                frame.Add(0x00);

            frame.Add(0x55);
            return frame.ToArray();
        }

        // v0.1 Stream Control Methods - Semantic IDs
        public bool StartStream(TransmissionRate rate)
        {
            byte[] data = new byte[1];
            data[0] = (byte)rate;  // Rate selection
            
            bool success = SendMessage(CAN_MSG_ID_START_STREAM, data);
            if (success) 
            {
                _isStreaming = true;
                RequestSystemStatus(); // Update UI with latest status immediately
            }
            return success;
        }

        public bool StopAllStreams()
        {
            // Empty message (0 bytes) for stop all streams
            bool success = SendMessage(CAN_MSG_ID_STOP_ALL_STREAMS, new byte[0]);
            if (success) 
            {
                _isStreaming = false;
                RequestSystemStatus(); // Update UI with latest status immediately
            }
            return success;
        }

        public bool SwitchToInternalADC()
        {
            // Empty message (0 bytes) for mode switch
            bool success = SendMessage(CAN_MSG_ID_MODE_INTERNAL, new byte[0]);
            if (success)
            {
                _eventDispatcher.CurrentADCMode = AdcMode.InternalWeight; // Update mode immediately for correct parsing
                RequestSystemStatus(); // Update UI with latest status immediately
            }
            return success;
        }

        public bool SwitchToADS1115()
        {
            // Empty message (0 bytes) for mode switch
            bool success = SendMessage(CAN_MSG_ID_MODE_ADS1115, new byte[0]);
            if (success)
            {
                _eventDispatcher.CurrentADCMode = AdcMode.Ads1115; // Update mode immediately for correct parsing
                RequestSystemStatus(); // Update UI with latest status immediately
            }
            return success;
        }

        /// <summary>
        /// Switch system mode (Weight vs Brake)
        /// </summary>
        /// <param name="mode">SystemMode enum value</param>
        public bool SwitchSystemMode(SystemMode mode)
        {
            byte[] data = new byte[] { (byte)mode };
            // Set System Mode (0x050)
            bool success = SendMessage(CAN_MSG_ID_SET_SYSTEM_MODE, data);
            if (success)
            {
                RequestSystemStatus(); // Update UI with latest status immediately
            }
            return success;
        }

        /// <summary>
        /// Request system status from STM32 (on-demand)
        /// </summary>
        /// <returns>True if request sent successfully</returns>
        public bool RequestSystemStatus(bool log = true)
        {
            return SendMessage(CAN_MSG_ID_STATUS_REQUEST, new byte[0], log);
        }

        /// <summary>
        /// Request firmware version from STM32 (on-demand)
        /// </summary>
        /// <returns>True if request sent successfully</returns>
        public bool RequestFirmwareVersion()
        {
            return SendMessage(CAN_MSG_ID_VERSION_REQUEST, new byte[0]);
        }



        /// <summary>
        /// Set data timeout for CAN communication
        /// </summary>
        /// <param name="timeout">Timeout duration</param>
        public void SetTimeout(TimeSpan timeout)
        {
            if (timeout.TotalSeconds < 1 || timeout.TotalSeconds > 300)
            {
                ProductionLogger.Instance.LogWarning($"Invalid timeout value: {timeout.TotalSeconds}s (must be 1-300 seconds)", "CANService");
                return;
            }
            
            _timeout = timeout;
            ProductionLogger.Instance.LogInfo($"CAN data timeout set to {timeout.TotalSeconds} seconds", "CANService");
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }

    }  // Class closing brace

    // v0.1 Event Args Classes - Ultra-Minimal
    public class RawDataEventArgs : EventArgs
    {
        // Side property removed as part of Total Weight refactoring
        // Raw ADC sum: int to support both modes
        // - Internal ADC: unsigned 0-16380 (stored as int, treated as unsigned, 4�4095)
        // - ADS1115: signed -131072 to +131068 (4 channels)
        public int RawADCSum { get; set; }
        public DateTime TimestampFull { get; set; } // PC3 reception timestamp
    }

    public class SystemStatusEventArgs : EventArgs
    {
        public SystemStatus SystemStatus { get; set; }      // 0=OK, 1=Warning, 2=Error
        public byte ErrorFlags { get; set; }        // Error flags
        public AdcMode ADCMode { get; set; }        // Current ADC mode
        public SystemMode RelayState { get; set; }        // Current relay state (0=Weight, 1=Brake)
        public uint UptimeSeconds { get; set; }     // System uptime in seconds
        public DateTime Timestamp { get; set; }     // PC3 reception timestamp
    }

    public class PerformanceMetricsEventArgs : EventArgs
    {
        public ushort CanTxHz { get; set; }        // CAN transmission frequency
        public ushort AdcSampleHz { get; set; }    // ADC sampling frequency
        public DateTime Timestamp { get; set; }     // PC3 reception timestamp
    }

    public class FirmwareVersionEventArgs : EventArgs
    {
        public byte Major { get; set; }              // Major version number
        public byte Minor { get; set; }              // Minor version number
        public byte Patch { get; set; }              // Patch version number
        public byte Build { get; set; }              // Build number
        public DateTime Timestamp { get; set; }      // PC3 reception timestamp
        
        public string VersionString => $"{Major}.{Minor}.{Patch}";
        public string VersionStringFull => $"{Major}.{Minor}.{Patch}.{Build}";
    }

    public class BootPingResponseEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
    }
    
    public class BootBeginResponseEventArgs : EventArgs
    {
        public BootloaderStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class BootProgressEventArgs : EventArgs
    {
        public byte Percent { get; set; }
        public uint BytesReceived { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class BootEndResponseEventArgs : EventArgs
    {
        public BootloaderStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class BootErrorEventArgs : EventArgs
    {
        public uint CanId { get; set; }
        public byte[]? RawData { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message => RawData != null ? BootloaderProtocol.ParseErrorMessage(CanId, RawData) : "Unknown Error";
    }
    
    public class BootQueryResponseEventArgs : EventArgs
    {
        public bool Present { get; set; }
        public byte Major { get; set; }
        public byte Minor { get; set; }
        public byte Patch { get; set; }
        public byte ActiveBank { get; set; }
        public byte BankAValid { get; set; }
        public byte BankBValid { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CANErrorEventArgs : EventArgs
    {
        public string? ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
        public DateTime Timestamp { get; set; }
    }

}  // Namespace closing brace