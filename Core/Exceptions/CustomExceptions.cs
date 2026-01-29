using System;

namespace ATS_TwoWheeler_WPF.Core.Exceptions
{
    /// <summary>
    /// Base exception for all CAN-related errors
    /// </summary>
    public class CANException : Exception
    {
        public CANException(string message) : base(message) { }
        public CANException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception thrown when CAN connection fails
    /// </summary>
    public class CANConnectionException : CANException
    {
        public string? PortName { get; }

        public CANConnectionException(string portName, string message)
            : base($"Failed to connect to CAN port '{portName}': {message}")
        {
            PortName = portName;
        }

        public CANConnectionException(string portName, string message, Exception inner)
            : base($"Failed to connect to CAN port '{portName}': {message}", inner)
        {
            PortName = portName;
        }
    }

    /// <summary>
    /// Exception thrown when CAN operation times out
    /// </summary>
    public class CANTimeoutException : CANException
    {
        public TimeSpan Timeout { get; }

        public CANTimeoutException(TimeSpan timeout)
            : base($"CAN operation timed out after {timeout.TotalSeconds:F1} seconds")
        {
            Timeout = timeout;
        }

        public CANTimeoutException(string operation, TimeSpan timeout)
            : base($"CAN {operation} timed out after {timeout.TotalSeconds:F1} seconds")
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// Exception thrown when CAN message sending fails
    /// </summary>
    public class CANSendException : CANException
    {
        public uint MessageId { get; }

        public CANSendException(uint messageId, string message)
            : base($"Failed to send CAN message 0x{messageId:X3}: {message}")
        {
            MessageId = messageId;
        }
    }

    /// <summary>
    /// Base exception for firmware-related errors
    /// </summary>
    public class FirmwareException : Exception
    {
        public FirmwareException(string message) : base(message) { }
        public FirmwareException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception thrown when firmware file is invalid
    /// </summary>
    public class FirmwareValidationException : FirmwareException
    {
        public string FilePath { get; }

        public FirmwareValidationException(string filePath, string reason)
            : base($"Invalid firmware file '{filePath}': {reason}")
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// Exception thrown when firmware size exceeds limits
    /// </summary>
    public class FirmwareSizeException : FirmwareException
    {
        public long FileSize { get; }
        public long MaxSize { get; }

        public FirmwareSizeException(long fileSize, long maxSize)
            : base($"Firmware size {fileSize} bytes exceeds maximum {maxSize} bytes")
        {
            FileSize = fileSize;
            MaxSize = maxSize;
        }
    }

    /// <summary>
    /// Exception thrown when firmware update fails
    /// </summary>
    public class FirmwareUpdateException : FirmwareException
    {
        public FirmwareUpdateException(string message) : base(message) { }
        public FirmwareUpdateException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Base exception for bootloader-related errors
    /// </summary>
    public class BootloaderException : Exception
    {
        public BootloaderException(string message) : base(message) { }
        public BootloaderException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Exception thrown when bootloader is not responding
    /// </summary>
    public class BootloaderNotRespondingException : BootloaderException
    {
        public BootloaderNotRespondingException()
            : base("Bootloader is not responding. Ensure device is in bootloader mode.") { }

        public BootloaderNotRespondingException(string details)
            : base($"Bootloader is not responding: {details}") { }
    }

    /// <summary>
    /// Exception thrown when bootloader operation fails
    /// </summary>
    public class BootloaderOperationException : BootloaderException
    {
        public string Operation { get; }

        public BootloaderOperationException(string operation, string message)
            : base($"Bootloader {operation} failed: {message}")
        {
            Operation = operation;
        }
    }
}
