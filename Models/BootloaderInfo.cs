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
        /// Firmware version from active bank
        /// </summary>
        public Version? FirmwareVersion { get; set; }

        /// <summary>
        /// Timestamp of last firmware update
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// Information about Bank A
        /// </summary>
        public BankInfo BankA { get; set; } = new BankInfo { BankNumber = 0 };

        /// <summary>
        /// Information about Bank B
        /// </summary>
        public BankInfo BankB { get; set; } = new BankInfo { BankNumber = 1 };

        /// <summary>
        /// Currently active bank (0 = Bank A, 1 = Bank B)
        /// </summary>
        public byte ActiveBank { get; set; }

        /// <summary>
        /// Active bank name for display
        /// </summary>
        public string ActiveBankName => ActiveBank == 0 ? "Bank A" : "Bank B";

        /// <summary>
        /// Status description for display
        /// </summary>
        public string StatusDescription => Core.BootloaderProtocol.DescribeStatus(Status);

        /// <summary>
        /// Get the active bank info
        /// </summary>
        public BankInfo GetActiveBankInfo() => ActiveBank == 0 ? BankA : BankB;

        /// <summary>
        /// Get the inactive bank info (target for updates)
        /// </summary>
        public BankInfo GetInactiveBankInfo() => ActiveBank == 0 ? BankB : BankA;
    }
}

