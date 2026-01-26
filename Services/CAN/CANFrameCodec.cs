using System;
using ATS_TwoWheeler_WPF.Models;

namespace ATS_TwoWheeler_WPF.Services.CAN
{
    /// <summary>
    /// Handles CAN frame encoding and decoding
    /// </summary>
    public static class CANFrameCodec
    {
        private const byte FrameStart = 0xAA;
        private const byte FrameEnd = 0x55;
        private const int FrameSize = 20;
        private const int MinFrameSize = 18;

        /// <summary>
        /// Decode a raw CAN frame into CAN ID and data
        /// </summary>
        public static (uint canId, byte[] canData) DecodeFrame(byte[] frame)
        {
            if (frame == null || frame.Length < MinFrameSize)
                throw new ArgumentException($"Frame too short: {frame?.Length ?? 0} bytes (min {MinFrameSize})");

            if (frame[0] != FrameStart)
                throw new ArgumentException($"Invalid frame start byte: 0x{frame[0]:X2} (expected 0x{FrameStart:X2})");

            // Extract CAN ID from bytes 2-3 (little-endian)
            uint canId = (uint)(frame[2] | (frame[3] << 8));

            // Extract data length from byte 1 (lower 4 bits)
            int dataLength = frame[1] & 0x0F;

            // Extract data bytes (bytes 4-11, up to dataLength)
            byte[] canData = new byte[dataLength];
            Array.Copy(frame, 4, canData, 0, Math.Min(dataLength, 8));

            return (canId, canData);
        }

        /// <summary>
        /// Create a CAN frame from CAN ID and data
        /// </summary>
        public static byte[] CreateFrame(uint id, byte[] data)
        {
            if (id > 0x7FF)
                throw new ArgumentException($"CAN ID 0x{id:X} exceeds 11-bit maximum (0x7FF)");

            if (data != null && data.Length > 8)
                throw new ArgumentException($"CAN data length {data.Length} exceeds maximum 8 bytes");

            var frame = new System.Collections.Generic.List<byte>
            {
                FrameStart,
                (byte)(0xC0 | Math.Min(data?.Length ?? 0, 8)),
                (byte)(id & 0xFF),
                (byte)((id >> 8) & 0xFF)
            };

            // Add data bytes (up to 8)
            frame.AddRange((data ?? new byte[0]).Take(8));
            
            // Pad to 12 bytes total (4 header + 8 data)
            while (frame.Count < 12)
                frame.Add(0x00);

            // Add frame end marker
            frame.Add(FrameEnd);

            return frame.ToArray();
        }

        /// <summary>
        /// Check if frame has valid start marker
        /// </summary>
        public static bool IsValidFrameStart(byte firstByte) => firstByte == FrameStart;

        /// <summary>
        /// Get expected frame size
        /// </summary>
        public static int GetFrameSize() => FrameSize;
    }
}
