namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Supported ADC Modes for the ATS System
    /// </summary>
    public enum AdcMode : byte
    {
        InternalWeight = 0,
        Ads1115 = 1
    }

    /// <summary>
    /// Supported System Modes for the ATS System
    /// </summary>
    public enum SystemMode : byte
    {
        Weight = 0,
        Brake = 1
    }
    /// System Status Codes
    /// </summary>
    public enum SystemStatus : byte
    {
        Ok = 0,
        Warning = 1,
        Error = 2,
        Unknown = 255
    }

    /// <summary>
    /// Weight Filtering Types
    /// </summary>
    public enum FilterType
    {
        None = 0,
        EMA = 1, // Exponential Moving Average
        SMA = 2  // Simple Moving Average
    }

    /// <summary>
    /// Calibration Modes
    /// </summary>
    public enum CalibrationMode
    {
        Regression = 0,
        Piecewise = 1
    }

    /// <summary>
    /// Data Logging Formats
    /// </summary>
    public enum LogFormat
    {
        CSV = 0,
        JSON = 1,
        TXT = 2
    }

    /// <summary>
    /// CAN Bus Baud Rates
    /// </summary>
    public enum CanBaudRate : byte
    {
        Bps125k = 0x00,
        Bps250k = 0x01,
        Bps500k = 0x02,
        Bps1M = 0x03
    }

    /// <summary>
    /// Data Transmission Rates (Streaming Frequency)
    /// </summary>
    public enum TransmissionRate : byte
    {
        Hz100 = 0x01,
        Hz500 = 0x02,
        Hz1000 = 0x03,
        Hz1 = 0x05
    }
}
