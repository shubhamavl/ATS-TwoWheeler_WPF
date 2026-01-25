using System;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// System status history entry
    /// </summary>
    public class StatusHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public byte SystemStatus { get; set; }      // 0=OK, 1=Warning, 2=Error
        public byte ErrorFlags { get; set; }        // Error flags
        public byte ADCMode { get; set; }           // 0=Internal, 1=ADS1115
        public byte RelayState { get; set; }        // 0=Weight, 1=Brake
        public ushort CanTxHz { get; set; }         // CAN TX Rate
        public ushort AdcSampleHz { get; set; }     // ADC Sample Rate
        public uint UptimeSeconds { get; set; }     // System Uptime in seconds
        public string FirmwareVersion { get; set; } = "--"; // Firmware Version
        
        public string StatusText => SystemStatus switch
        {
            0 => "OK",
            1 => "Warning",
            2 => "Error",
            3 => "Critical",
            _ => "Unknown"
        };
        
        public string ModeText => ADCMode switch
        {
            0 => "Internal ADC",
            1 => "ADS1115",
            _ => "Unknown"
        };
        
        public string ErrorFlagsText => $"0x{ErrorFlags:X2}";
    }
}

