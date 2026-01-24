using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.Adapters
{
    /// <summary>
    /// USB-CAN-A Serial adapter implementation using SerialPort
    /// </summary>
    public class UsbSerialCanAdapter : ICanAdapter
    {
        public string AdapterType => "USB-CAN-A Serial";

        private SerialPort? _serialPort;
        private readonly ConcurrentQueue<byte> _frameBuffer = new();
        private volatile bool _connected;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _sendLock = new object();
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private bool _timeoutNotified = false;

        // Protocol constants
        private const byte FRAME_HEADER = 0xAA;
        private const byte FRAME_FOOTER = 0x55;
        private const uint MAX_CAN_ID = 0x7FF; // 11-bit CAN ID limit

        public bool IsConnected => _connected;

        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public bool Connect(CanAdapterConfig config, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (config is not UsbSerialCanAdapterConfig usbConfig)
            {
                errorMessage = "Invalid configuration type for USB-CAN-A Serial adapter";
                return false;
            }

            try
            {
                if (string.IsNullOrEmpty(usbConfig.PortName))
                {
                    // Auto-detect COM port
                    string[] availablePorts = SerialPort.GetPortNames();
                    if (availablePorts.Length == 0)
                    {
                        errorMessage = "No COM ports found. Please check:\n• USB-CAN-A is connected\n• CH341 driver is installed\n• Device appears in Device Manager";
                        return false;
                    }
                    usbConfig.PortName = availablePorts[availablePorts.Length - 1];
                }

                _serialPort = new SerialPort(usbConfig.PortName, usbConfig.SerialBaudRate, Parity.None, 8, StopBits.One);
                
                // Configure timeouts to prevent blocking during high-speed transfers
                _serialPort.WriteTimeout = 100; // 100ms timeout instead of infinite (prevents blocking)
                _serialPort.ReadTimeout = 100;  // 100ms read timeout (may be removable if BytesToRead check prevents blocking)
                // Note: Handshake defaults to None, no need to set explicitly
                _serialPort.DtrEnable = true; // Enable DTR for better USB-CAN adapter compatibility
                _serialPort.RtsEnable = true; // Enable RTS for better USB-CAN adapter compatibility
                
                _serialPort.Open();
                _connected = true;

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ReadMessagesAsync(_cancellationTokenSource.Token));

                ConnectionStatusChanged?.Invoke(this, true);
                System.Diagnostics.Debug.WriteLine($"USB-CAN-A Connected on {usbConfig.PortName}");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                System.Diagnostics.Debug.WriteLine($"USB-CAN-A connection error: {ex.Message}");
                _connected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }
        }

        public void Disconnect()
        {
            _connected = false;
            _cancellationTokenSource?.Cancel();
            if (_serialPort?.IsOpen == true)
                _serialPort.Close();

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            ConnectionStatusChanged?.Invoke(this, false);
            System.Diagnostics.Debug.WriteLine("USB-CAN-A Disconnected");
        }

        public bool SendMessage(uint id, byte[] data)
        {
            if (!_connected || _serialPort == null) return false;

            try
            {
                // Validate CAN ID (11-bit max for standard frame)
                if (id > MAX_CAN_ID)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid CAN ID: 0x{id:X3} (max 0x{MAX_CAN_ID:X3} for standard frame)");
                    return false;
                }

                // Validate data length
                if (data != null && data.Length > 8)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid data length: {data.Length} (max 8 bytes)");
                    return false;
                }

                var frame = CreateFrame(id, data ?? new byte[0]);

                lock (_sendLock)
                {
                    _serialPort.Write(frame, 0, frame.Length);
                }

                // Fire event for TX messages
                var txMessage = new CANMessage(id, data ?? new byte[0], DateTime.Now, "TX");
                MessageReceived?.Invoke(txMessage);

                System.Diagnostics.Debug.WriteLine($"USB-CAN-A: Sent CAN frame ID=0x{id:X3}, Data={BitConverter.ToString(data ?? new byte[0])}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send message error: {ex.Message}");
                return false;
            }
        }

        public string[] GetAvailableOptions()
        {
            return SerialPort.GetPortNames();
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

                        // HEX DUMP for debugging: Log raw bytes arriving from serial
                        string hex = BitConverter.ToString(buffer, 0, count);
                        ProductionLogger.Instance.LogInfo($"RAW SERIAL: {hex}", "Adapter");

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
                    DataTimeout?.Invoke(this, "Timeout");
                }

                await Task.Delay(5, token);
            }
        }

        private void ProcessFrames()
        {
            // USB-CAN-A Protocol Detection
            // Based on logs, the hardware is sending FIXED 20-byte frames starting with AA 55.
            // Format: [AA] [55] [?] [?] [?] [ID_L] [ID_H] [?] [?] [?] [Data 0-7] [?] [Footer?]
            
            while (_frameBuffer.Count >= 5) 
            {
                byte[] bytes = _frameBuffer.ToArray();
                int headerIndex = -1;
                
                // Search for 0xAA (Packet Start Flag)
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0xAA)
                    {
                        headerIndex = i;
                        break;
                    }
                }
                
                // Fallback for simple 0xAA variable length header
                if (headerIndex == -1)
                {
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        if (bytes[i] == 0xAA)
                        {
                            headerIndex = i;
                            break;
                        }
                    }
                }

                if (headerIndex == -1)
                {
                    // No header found, clear buffer
                    while (_frameBuffer.TryDequeue(out _)) ;
                    return;
                }

                // Discard bytes before header
                for (int i = 0; i < headerIndex; i++)
                {
                    _frameBuffer.TryDequeue(out _);
                }

                // Refresh byte array after alignment
                bytes = _frameBuffer.ToArray();
                if (bytes.Length < 2) break; // Need at least Header+Type

                // Strict Variable Length Protocol (Waveshare)
                // [AA] [Type/DLC] [ID] ...
                if (bytes.Length < 2) break; // Need at least header and type to determine length

                // Sanity check: Header must be 0xAA
                if (bytes[0] != 0xAA)
                {
                    // Should be unreachable due to alignment above, but safe to check
                    _frameBuffer.TryDequeue(out _);
                    continue; 
                }

                byte typeByte = bytes[1];
                if ((typeByte & 0xF0) == 0) // Sanity check: Type usually starts with 0xC0 or 0xE0, or at least has high bits
                {
                     // If type byte looks wrong (e.g. 0x00), usually not a variable frame start.
                     // But strictly speaking, spec says bits 0-3 are DLC.
                }

                bool isExtended = (typeByte & 0x20) != 0;
                byte dlc = (byte)(typeByte & 0x0F);
                int overhead = isExtended ? 7 : 5; // Header(1) + Type(1) + ID(2 or 4) + Footer(1)
                int expectedLength = overhead + dlc;

                if (bytes.Length < expectedLength) break;

                // Validate footer
                if (bytes[expectedLength - 1] == 0x55)
                {
                    var frame = new byte[expectedLength];
                    for (int i = 0; i < expectedLength; i++) _frameBuffer.TryDequeue(out frame[i]);
                    DecodeFrame(frame);
                }
                else
                {
                    // Invalid frame sequence, drop header and retry
                    _frameBuffer.TryDequeue(out _);
                }
            }
        }

        private void DecodeFrame(byte[] frame)
        {
            try
            {
                uint canId = 0;
                byte[]? canData = null;
                byte dlc = 0;

                // Variable Length Protocol
                if (frame.Length >= 5 && frame[0] == 0xAA)
                {
                    byte typeByte = frame[1];
                    bool isExtended = (typeByte & 0x20) != 0;
                    dlc = (byte)(typeByte & 0x0F);
                    
                    if (isExtended)
                    {
                         canId = (uint)(frame[2] | (frame[3] << 8) | (frame[4] << 16) | (frame[5] << 24));
                         canData = new byte[dlc];
                         if (dlc > 0) Array.Copy(frame, 6, canData, 0, dlc);
                    }
                    else
                    {
                         canId = (uint)(frame[2] | (frame[3] << 8));
                         canData = new byte[dlc];
                         if (dlc > 0) Array.Copy(frame, 4, canData, 0, dlc);
                    }
                    
                    ProductionLogger.Instance.LogInfo($"Adapter RX (Var): ID=0x{canId:X} ({(isExtended?"EXT":"STD")}) Data={BitConverter.ToString(canData)}", "Adapter");
                }
                else
                {
                    return;
                }

                if (canData != null && IsTwoWheelerMessage(canId))
                {
                    var canMessage = new CANMessage(canId, canData);
                    MessageReceived?.Invoke(canMessage);
                }
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Adapter Decode Error: {ex.Message}", "Adapter");
            }
        }

        private bool IsTwoWheelerMessage(uint canId)
        {
            // Protocol v0.1 - ATS Two-Wheeler System - Semantic IDs
            switch (canId)
            {
                // Raw ADC Data Transmission (STM32 → PC3)
                case 0x200:  // Total raw ADC data (all 4 channels: Ch0+Ch1+Ch2+Ch3)

                // Stream Control Commands (PC3 → STM32, but we receive them too for monitoring)
                case 0x040:  // Start streaming (1 byte: rate)
                case 0x044:  // Stop all streams

                // System Control Messages (PC3 → STM32, but we receive them too for monitoring)
                case 0x030:  // Switch to Internal ADC mode
                case 0x031:  // Switch to ADS1115 mode
                case 0x032:  // Request system status
                case 0x033:  // Request firmware version
                case 0x050:  // Set system mode (1 byte: 0=Weight, 1=Brake)

                // System Status (STM32 → PC3)
                case 0x300:  // System status response
                case 0x301:  // Firmware version response

                // Bootloader Protocol (must match BootloaderProtocol / STM32)
                case 0x510:  // Enter Bootloader
                case 0x511:  // Query Boot Info
                case 0x512:  // Ping
                case 0x513:  // Begin Update
                case 0x514:  // End Update
                case 0x515:  // Reset
                case 0x516:  // Buffer Overflow Error
                case 0x517:  // Ping Response
                case 0x518:  // Begin Response
                case 0x519:  // Progress Update
                case 0x51A:  // End Response
                case 0x51B:  // Sequence Mismatch Error
                case 0x51C:  // Query Response
                case 0x51D:  // Size Mismatch Error
                case 0x51E:  // Write Error  
                case 0x51F:  // Validation Error
                case 0x520:  // Data frames

                // Legacy calibration responses (for backward compatibility)
                case 0x400:  // Variable calibration data
                case 0x401:  // Calibration quality analysis
                case 0x402:  // Error response
                    return true;

                default:
                    return false;
            }
        }

        private static byte[] CreateFrame(uint id, byte[] data)
        {
            // Variable-length protocol: [0xAA] [Type] [ID_LOW] [ID_HIGH] [DATA...] [0x55]
            // Type byte: bit5=0 (standard frame), bit4=0 (data frame), bits 0-3=DLC (0-8)
            byte dlc = (byte)Math.Min(data?.Length ?? 0, 8);
            
            var frame = new List<byte>
            {
                FRAME_HEADER,                                    // Byte 0: Header (0xAA)
                (byte)(0xC0 | dlc),                              // Byte 1: Type (0xC0 = standard, data, DLC)
                (byte)(id & 0xFF),                               // Byte 2: ID low
                (byte)((id >> 8) & 0xFF)                         // Byte 3: ID high
            };

            // Add data bytes (only actual data, no padding)
            if (data != null && dlc > 0)
            {
                frame.AddRange(data.Take(dlc));
            }

            // Add footer
            frame.Add(FRAME_FOOTER);                             // Last byte: Footer (0x55)
            
            return frame.ToArray();
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }
    }
}


