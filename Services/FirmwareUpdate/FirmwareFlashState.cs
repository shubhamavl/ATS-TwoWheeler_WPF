using System;

namespace ATS_TwoWheeler_WPF.Services.FirmwareUpdate
{
    /// <summary>
    /// Tracks the state of a firmware flash operation
    /// </summary>
    public class FirmwareFlashState
    {
        public long TotalBytes { get; set; }
        public long BytesSent { get; set; }
        public int TotalChunks { get; set; }
        public int ChunksSent { get; set; }
        public DateTime StartTime { get; set; }
        public uint CurrentCrc { get; set; }

        public double ProgressPercentage => TotalChunks > 0
            ? (ChunksSent * 100.0 / TotalChunks)
            : 0;

        public double BytesPerSecond
        {
            get
            {
                var elapsed = DateTime.Now - StartTime;
                return elapsed.TotalSeconds > 0
                    ? BytesSent / elapsed.TotalSeconds
                    : 0;
            }
        }

        public TimeSpan ElapsedTime => DateTime.Now - StartTime;

        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (ChunksSent == 0 || BytesPerSecond == 0)
                {
                    return TimeSpan.Zero;
                }

                var remainingBytes = TotalBytes - BytesSent;
                var remainingSeconds = remainingBytes / BytesPerSecond;
                return TimeSpan.FromSeconds(remainingSeconds);
            }
        }

        public void Reset()
        {
            TotalBytes = 0;
            BytesSent = 0;
            TotalChunks = 0;
            ChunksSent = 0;
            StartTime = DateTime.Now;
            CurrentCrc = 0xFFFFFFFF;
        }

        public void UpdateProgress(int chunksSent, long bytesSent)
        {
            ChunksSent = chunksSent;
            BytesSent = bytesSent;
        }
    }
}
