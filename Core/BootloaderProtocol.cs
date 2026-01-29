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
        public const uint CanIdBootData = 0x520;        // Data frames (8 bytes: seq + 7 data bytes)
        public const uint CanIdBootPingResponse = 0x517;    // Ping Response (READY)
        public const uint CanIdBootBeginResponse = 0x518;    // Begin Response (IN_PROGRESS/FAILED)
        public const uint CanIdBootProgress = 0x519;          // Progress Update
        public const uint CanIdBootEndResponse = 0x51A;      // End Response (SUCCESS/FAILED)
        public const uint CanIdBootError = 0x51B;             // Sequence Mismatch (Retry)
        public const uint CanIdErrSize = 0x51D;              // Size Mismatch
        public const uint CanIdErrWrite = 0x51E;             // Flash Write Failed
        public const uint CanIdErrValidation = 0x51F;        // Validation Failed
        public const uint CanIdErrBuffer = 0x516;            // Buffer Overflow
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
                _ => $"Unknown (0x{(byte)status:X2})",
            };
        }

        public static string ParseErrorMessage(uint canId, byte[] data)
        {
            if (data == null)
            {
                return "Unknown Error";
            }

            switch (canId)
            {
                case CanIdBootError: // Sequence Mismatch
                    if (data.Length >= 2)
                    {
                        byte expected = data[0];
                        byte received = data[1];
                        return $"Sequence Mismatch: Expected {expected}, Received {received} (Auto-retrying)";
                    }
                    return "Sequence Mismatch";

                case CanIdErrSize:
                    if (data.Length >= 8)
                    {
                        uint expected = BitConverter.ToUInt32(data, 0);
                        uint received = BitConverter.ToUInt32(data, 4);
                        return $"Size Mismatch: Expected {expected} bytes, Received {received} bytes";
                    }
                    return "Size Mismatch";

                case CanIdErrWrite:
                    if (data.Length >= 4)
                    {
                        if (data[0] == 'E') // Erase error (custom format)
                        {
                            uint address = BitConverter.ToUInt32(data, 1);
                            return $"Flash Erase Failed at 0x{address:X8}";
                        }
                        uint addr = BitConverter.ToUInt32(data, 0);
                        return $"Flash Write Failed at 0x{addr:X8}";
                    }
                    return "Flash Write Failed";

                case CanIdErrValidation:
                    if (data.Length >= 8)
                    {
                        // Now we get the FULL 8 bytes!
                        uint stack = BitConverter.ToUInt32(data, 0);
                        uint reset = BitConverter.ToUInt32(data, 4);
                        return $"Validation Failed: Stack=0x{stack:X8}, Reset=0x{reset:X8} (Expect 0x2000xxxx, 0x08008xxx)";
                    }
                    return "Firmware Validation Failed (Invalid Header)";

                case CanIdErrBuffer:
                    return "CAN Buffer Overflow (PC is sending too fast)";

                default:
                    return $"Error (ID: 0x{canId:X3})";
            }
        }


    }
}



