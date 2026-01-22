using System;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Represents comprehensive bootloader and firmware information
    /// </summary>
    public class BootloaderInfo
    {
        /// <summary>
        /// Whether bootloader is present and responding
        /// </summary>
        public bool IsPresent { get; set; }

        /// <summary>
        /// Current bootloader status
        /// </summary>
        public BootloaderStatus Status { get; set; }

        /// <summary>
        /// Status description for display
        /// </summary>
        public string StatusDescription => Core.BootloaderProtocol.DescribeStatus(Status);

        /// <summary>
        /// Firmware version from active bank
        /// </summary>
        public Version? FirmwareVersion { get; set; }

        /// <summary>
        /// Timestamp of last firmware update
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// Information about the Application Bank (Bank A)
        /// </summary>
        public BankInfo Bank { get; set; } = new BankInfo { BankNumber = 0 };
    }
}

