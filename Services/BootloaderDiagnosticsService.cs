using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services
{
    /// <summary>
    /// Service for bootloader diagnostics, message capture, and troubleshooting
    /// </summary>
    public class BootloaderDiagnosticsService : IBootloaderDiagnosticsService
    {
        private readonly ProductionLogger _logger = ProductionLogger.Instance;
        private readonly ObservableCollection<BootloaderMessage> _messages = new ObservableCollection<BootloaderMessage>();
        private readonly List<BootloaderError> _errors = new List<BootloaderError>();

        public ObservableCollection<BootloaderMessage> Messages => _messages;
        public IReadOnlyList<BootloaderError> Errors => _errors.AsReadOnly();

        /// <summary>
        /// Capture a bootloader CAN message
        /// </summary>
        public void CaptureMessage(uint canId, byte[] data, bool isTx)
        {
            // Only capture bootloader control/error messages (excluding high-volume 0x520 Data frames)
            if (canId >= 0x510 && canId < 0x520)
            {
                var message = new BootloaderMessage
                {
                    Timestamp = DateTime.Now,
                    CanId = canId,
                    Data = (byte[])data.Clone(),
                    IsTx = isTx,
                    Direction = isTx ? "TX" : "RX"
                };

                // Parse message content for display
                message.Description = ParseMessage(canId, data, isTx);

                _messages.Add(message);

                // Keep only last 1000 messages
                if (_messages.Count > 1000)
                {
                    _messages.RemoveAt(0);
                }

                // Check for errors in response messages
                if (canId == BootloaderProtocol.CanIdBootBeginResponse && data.Length > 0)
                {
                    var status = (BootloaderStatus)data[0];
                    if (status == BootloaderStatus.FailedFlash)
                    {
                        RecordError(status, message);
                    }
                }
                else if (canId == BootloaderProtocol.CanIdBootEndResponse && data.Length > 0)
                {
                    var status = (BootloaderStatus)data[0];
                    if (status == BootloaderStatus.FailedChecksum || status == BootloaderStatus.FailedFlash)
                    {
                        RecordError(status, message);
                    }
                }
                else if (canId == BootloaderProtocol.CanIdBootError || 
                         canId == BootloaderProtocol.CanIdErrSize ||
                         canId == BootloaderProtocol.CanIdErrWrite ||
                         canId == BootloaderProtocol.CanIdErrValidation ||
                         canId == BootloaderProtocol.CanIdErrBuffer)
                {
                    RecordSpecificError(canId, data, message);
                }
            }
        }

        /// <summary>
        /// Parse a bootloader message for display
        /// </summary>
        private string ParseMessage(uint canId, byte[] data, bool isTx)
        {
            if (data == null || data.Length == 0)
                return "Empty message";

            switch (canId)
            {
                case BootloaderProtocol.CanIdBootEnter:
                    return "Enter Bootloader";

                case BootloaderProtocol.CanIdBootQueryInfo:
                    return "Query Boot Info";

                case BootloaderProtocol.CanIdBootPing:
                    return "Ping";

                case BootloaderProtocol.CanIdBootBegin:
                    if (data.Length >= 4)
                    {
                        uint size = BitConverter.ToUInt32(data, 0);
                        return $"Begin Update: {size} bytes";
                    }
                    return "Begin Update";

                case BootloaderProtocol.CanIdBootEnd:
                    if (data.Length >= 4)
                    {
                        uint crc = BitConverter.ToUInt32(data, 0);
                        return $"End Update: CRC=0x{crc:X8}";
                    }
                    return "End Update";

                case BootloaderProtocol.CanIdBootReset:
                    return "Reset";

                case BootloaderProtocol.CanIdBootData:
                    if (data.Length > 0)
                    {
                        byte seq = data[0];
                        int dataBytes = Math.Min(7, data.Length - 1);
                        return $"Data Frame: Seq={seq}, Bytes={dataBytes}";
                    }
                    return "Data Frame";

                case BootloaderProtocol.CanIdBootPingResponse:
                    return "Ping Response: READY";

                case BootloaderProtocol.CanIdBootBeginResponse:
                    if (data.Length > 0)
                    {
                        var status = (BootloaderStatus)data[0];
                        return $"Begin Response: {BootloaderProtocol.DescribeStatus(status)}";
                    }
                    return "Begin Response";

                case BootloaderProtocol.CanIdBootProgress:
                    if (data.Length >= 6)
                    {
                        byte percent = data[0];
                        uint received = BitConverter.ToUInt32(data, 1);
                        return $"Progress: {percent}%, {received} bytes";
                    }
                    return "Progress Update";

                case BootloaderProtocol.CanIdBootEndResponse:
                    if (data.Length > 0)
                    {
                        var status = (BootloaderStatus)data[0];
                        return $"End Response: {BootloaderProtocol.DescribeStatus(status)}";
                    }
                    return "End Response";

                case BootloaderProtocol.CanIdBootError:
                case BootloaderProtocol.CanIdErrSize:
                case BootloaderProtocol.CanIdErrWrite:
                case BootloaderProtocol.CanIdErrValidation:
                case BootloaderProtocol.CanIdErrBuffer:
                    return BootloaderProtocol.ParseErrorMessage(canId, data);

                case BootloaderProtocol.CanIdBootQueryResponse:
                    if (data.Length >= 4)
                    {
                        return $"Query Response: v{data[1]}.{data[2]}.{data[3]}";
                    }
                    return "Query Response";

                default:
                    return $"Unknown: 0x{canId:X3}";
            }
        }

        /// <summary>
        /// Record a specific error for analysis (new multi-ID protocol)
        /// </summary>
        private void RecordSpecificError(uint canId, byte[] data, BootloaderMessage message)
        {
            var error = new BootloaderError
            {
                Timestamp = message.Timestamp,
                ErrorCode = (byte)(canId & 0xFF), // Just for display
                Description = BootloaderProtocol.ParseErrorMessage(canId, data),
                Message = message,
                SuggestedResolution = GetSuggestedResolutionFromCanId(canId)
            };

            _errors.Add(error);

            if (_errors.Count > 100)
            {
                _errors.RemoveAt(0);
            }

            _logger.LogError($"Bootloader specific error: {error.Description}", "BootloaderDiagnostics");
        }

        private void RecordError(BootloaderStatus status, BootloaderMessage message)
        {
            var error = new BootloaderError
            {
                Timestamp = message.Timestamp,
                ErrorCode = (byte)status,
                Description = BootloaderProtocol.DescribeStatus(status),
                Message = message,
                SuggestedResolution = GetSuggestedResolution(status)
            };

            _errors.Add(error);

            // Keep only last 100 errors
            if (_errors.Count > 100)
            {
                _errors.RemoveAt(0);
            }

            _logger.LogError($"Bootloader error: {error.Description}", "BootloaderDiagnostics");
        }

        /// <summary>
        /// Get suggested resolution for a specific error ID
        /// </summary>
        private string GetSuggestedResolutionFromCanId(uint canId)
        {
            switch (canId)
            {
                case BootloaderProtocol.CanIdBootError:
                    return "Sequence mismatch. A data packet was lost. The system is attempting to retry automatically.";
                case BootloaderProtocol.CanIdErrSize:
                    return "Size mismatch. The transferred bytes don't match the expected size. Check the binary file.";
                case BootloaderProtocol.CanIdErrWrite:
                    return "Flash operation failed. Memory may be worn or protected. Check hardware connection.";
                case BootloaderProtocol.CanIdErrValidation:
                    return "Firmware validation failed. The header or memory layout is invalid for this chip.";
                case BootloaderProtocol.CanIdErrBuffer:
                    return "CAN buffer overflow. Data is being sent too fast. Increase the delay between packets.";
                default:
                    return "Unknown bootloader error. Check CAN connection and try again.";
            }
        }

        /// <summary>
        /// Get suggested resolution for an error
        /// </summary>
        private string GetSuggestedResolution(BootloaderStatus status)
        {
            return status switch
            {
                BootloaderStatus.FailedChecksum => 
                    "Checksum mismatch detected. Possible causes: CAN bus interference, corrupted firmware file, or missing data frames. Try updating again with stable power and CAN connection.",
                BootloaderStatus.FailedTimeout => 
                    "Update timed out. Possible causes: CAN bus disconnected, slow transmission, or STM32 reset. Check CAN connection and try again.",
                BootloaderStatus.FailedFlash => 
                    "Flash write error. Possible causes: Flash memory wear, power loss during write, or invalid address. Try updating again or check flash integrity.",
                _ => "Unknown error. Check CAN connection and try again."
            };
        }

        /// <summary>
        /// Clear all captured messages
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
        }

        /// <summary>
        /// Clear all errors
        /// </summary>
        public void ClearErrors()
        {
            _errors.Clear();
        }

        /// <summary>
        /// Export messages - interface method
        /// </summary>
        public string ExportMessages() => ExportMessagesToText();
        
        /// <summary>
        /// Export messages to text file
        /// </summary>
        public string ExportMessagesToText()
        {
            var lines = new List<string>
            {
                "Bootloader CAN Messages",
                "======================",
                $"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                ""
            };

            foreach (var msg in _messages)
            {
                string hexData = BitConverter.ToString(msg.Data).Replace("-", " ");
                lines.Add($"[{msg.Timestamp:HH:mm:ss.fff}] {msg.Direction} 0x{msg.CanId:X3}: {msg.Description}");
                lines.Add($"  Data: {hexData}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Represents a captured bootloader CAN message
    /// </summary>
    public class BootloaderMessage
    {
        public DateTime Timestamp { get; set; }
        public uint CanId { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public bool IsTx { get; set; }
        public string Direction { get; set; } = "";
        public string Description { get; set; } = "";
        
        /// <summary>
        /// Hex representation of the data bytes for display in the UI grid
        /// </summary>
        public string DataHex => Data.Length > 0 
            ? BitConverter.ToString(Data).Replace("-", " ") 
            : "";
    }

    /// <summary>
    /// Represents a bootloader error with diagnostic information
    /// </summary>
    public class BootloaderError
    {
        public DateTime Timestamp { get; set; }
        public byte ErrorCode { get; set; }
        public string Description { get; set; } = "";
        public BootloaderMessage? Message { get; set; }
        public string SuggestedResolution { get; set; } = "";
    }
}

