using System;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Raw weight data from CAN (ATS Two-Wheeler)
    /// Supports both Internal ADC (unsigned 0-16380 for 4 channels) and ADS1115 (signed -131072 to +131068)
    /// </summary>
    public class RawWeightData
    {
        // Side property removed as part of Total Weight refactoring
        public int RawADC { get; set; }  // Combined Ch0+Ch1+Ch2+Ch3 (signed for ADS1115, unsigned for Internal)
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Processed weight data with calibration and tare applied
    /// </summary>
    public class ProcessedWeightData
    {
        public int RawADC { get; set; }  // Changed from ushort to int for ADS1115 signed support
        public double CalibratedWeight { get; set; }
        public double TaredWeight { get; set; }
        public double TareValue { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

