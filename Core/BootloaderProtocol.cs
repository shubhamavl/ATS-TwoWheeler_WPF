namespace ATS_TwoWheeler_WPF.Core
{
    // Bootloader status enum (public for use in models)
    public enum BootloaderStatus : byte
    {
        Idle = 0,
        Ready = 1,
        InProgress = 2,
        Success = 3,
        FailedChecksum = 4,
        FailedTimeout = 5,
        FailedFlash = 6,
    }

    internal static class BootloaderProtocol
    {
        // CAN IDs matching STM32 bootloader implementation (using separate CAN IDs instead of data bytes)
        public const uint CanIdBootEnter = 0x510;      // Enter Bootloader (no data bytes)
        public const uint CanIdBootQueryInfo = 0x511;   // Query Boot Info (no data bytes)
        public const uint CanIdBootPing = 0x512;       // Ping (no data bytes)
        public const uint CanIdBootBegin = 0x513;       // Begin Update (4 bytes: firmware size)
        public const uint CanIdBootEnd = 0x514;         // End Update (4 bytes: CRC32)
        public const uint CanIdBootReset = 0x515;       // Reset (no data bytes)
        public const uint CanIdBootData = 0x516;        // Data frames (8 bytes: seq + 7 data bytes)
        public const uint CanIdBootPingResponse = 0x517;    // Ping Response (READY)
        public const uint CanIdBootBeginResponse = 0x518;    // Begin Response (IN_PROGRESS/FAILED)
        public const uint CanIdBootProgress = 0x519;          // Progress Update
        public const uint CanIdBootEndResponse = 0x51A;      // End Response (SUCCESS/FAILED)
        public const uint CanIdBootError = 0x51B;             // Error Response (all errors)
        public const uint CanIdBootQueryResponse = 0x51C;    // Query Response

        public static string DescribeStatus(BootloaderStatus status)
        {
            return status switch
            {
                BootloaderStatus.Idle => "Idle",
                BootloaderStatus.Ready => "Ready",
                BootloaderStatus.InProgress => "Updating...",
                BootloaderStatus.Success => "Last update succeeded",
                BootloaderStatus.FailedChecksum => "Checksum failed",
                BootloaderStatus.FailedTimeout => "Timeout while updating",
                BootloaderStatus.FailedFlash => "Flash error",
                _ => "Unknown",
            };
        }
    }
}

