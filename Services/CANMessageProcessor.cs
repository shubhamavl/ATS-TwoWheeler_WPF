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

            // Fixed parsing to match UsbSerialCanAdapter logic (Standard frame: ID at indices 2/3)
            // Note: This assumes standard frame (Type 0xC8 or similar). 
            // If the buffer was aligned by UsbSerialCanAdapter.ProcessFrames, it starts with AA.
            // Index 0: AA
            // Index 1: Type
            // Index 2: ID_L
            // Index 3: ID_H
            uint canId = (uint)(frame[2] | (frame[3] << 8));
            byte[] canData = new byte[8];
            // Data starts at Index 4? No, UsbSerialCanAdapter.CreateFrame puts data at index 4.
            // Let's check UsbSerialCanAdapter.ProcessFrames again.
            // It extracts 'expectedLength' bytes.
            // Standard frame overhead is 5 bytes (AA + Type + ID + ID + Footer).
            // Data is at Index 4.
            
            // Wait, UsbSerialCanAdapter.DecodeFrame (RX) uses:
            // canId = frame[2] | frame[3] << 8
            // canData = copy from frame[4]
            
            // BUT CANService.DecodeFrame takes 20 bytes from buffer blindly?
            // "var frame = new byte[20]; ... DecodeFrame(frame);"
            // And CreateFrame produces 12 bytes total (padded to 12).
            // If CANService.ProcessFrames aligns to 0xAA, then:
            // 0: AA
            // 1: Type
            // 2: ID
            // 3: ID
            // 4..11: Data (8 bytes)
            // 12..: Padding/Footer
            
            // So Data is at 4.
            Array.Copy(frame, 4, canData, 0, 8);

            return (canId, canData);
        }
    }
}
