using System;
using System.Collections.Generic;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public class CANMessageProcessor
    {
        // CAN Message IDs - Semantic IDs
        public const uint CAN_MSG_ID_TOTAL_RAW_DATA = 0x200;
        public const uint CAN_MSG_ID_START_STREAM = 0x040;
        public const uint CAN_MSG_ID_STOP_ALL_STREAMS = 0x044;
        public const uint CAN_MSG_ID_SYSTEM_STATUS = 0x300;
        public const uint CAN_MSG_ID_SYS_PERF = 0x302;
        public const uint CAN_MSG_ID_STATUS_REQUEST = 0x032;
        public const uint CAN_MSG_ID_MODE_INTERNAL = 0x030;
        public const uint CAN_MSG_ID_MODE_ADS1115 = 0x031;
        public const uint CAN_MSG_ID_VERSION_REQUEST = 0x033;
        public const uint CAN_MSG_ID_SET_SYSTEM_MODE = 0x050;
        public const uint CAN_MSG_ID_VERSION_RESPONSE = 0x301;

        public static bool IsTwoWheelerMessage(uint canId)
        {
            switch (canId)
            {
                case CAN_MSG_ID_TOTAL_RAW_DATA:
                case CAN_MSG_ID_START_STREAM:
                case CAN_MSG_ID_STOP_ALL_STREAMS:
                case CAN_MSG_ID_SYSTEM_STATUS:
                case CAN_MSG_ID_SYS_PERF:
                case CAN_MSG_ID_STATUS_REQUEST:
                case CAN_MSG_ID_MODE_INTERNAL:
                case CAN_MSG_ID_MODE_ADS1115:
                case CAN_MSG_ID_VERSION_REQUEST:
                case CAN_MSG_ID_SET_SYSTEM_MODE:
                case CAN_MSG_ID_VERSION_RESPONSE:
                    return true;
                default:
                    // Check bootloader IDs separately or include here
                    return IsBootloaderMessage(canId);
            }
        }

        public static bool IsBootloaderMessage(uint canId)
        {
            return canId >= 0x510 && canId <= 0x520;
        }

        public static (uint id, byte[] data) DecodeFrame(byte[] frame)
        {
            if (frame.Length < 18 || frame[0] != 0xAA)
            {
                throw new ArgumentException("Invalid frame format");
            }

            uint canId = (uint)(frame[5] | (frame[6] << 8));
            byte[] canData = new byte[8];
            Array.Copy(frame, 10, canData, 0, 8);

            return (canId, canData);
        }
    }
}
