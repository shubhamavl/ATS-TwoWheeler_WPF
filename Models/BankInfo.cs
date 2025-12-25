using System;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Represents information about a firmware bank (Bank A or Bank B)
    /// </summary>
    public class BankInfo
    {
        /// <summary>
        /// Whether this bank contains valid firmware
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Firmware version in this bank
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// CRC32 checksum of the firmware in this bank
        /// </summary>
        public uint Crc { get; set; }

        /// <summary>
        /// Size of firmware in bytes
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// Timestamp of last update to this bank
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// Bank number (0 = Bank A, 1 = Bank B)
        /// </summary>
        public byte BankNumber { get; set; }

        /// <summary>
        /// Human-readable bank name
        /// </summary>
        public string BankName => BankNumber == 0 ? "Bank A" : "Bank B";

        /// <summary>
        /// Status string for display
        /// </summary>
        public string StatusString => IsValid ? "✓ Valid" : "✗ Invalid";

        /// <summary>
        /// Version string for display
        /// </summary>
        public string VersionString => Version?.ToString() ?? "Unknown";
    }
}

