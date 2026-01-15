using System;

namespace ATS_TwoWheeler_WPF.Models
{
    public class BrakeTestDataModel
    {
        public string TestId { get; set; } = string.Empty;
        public DateTime TestStartTime { get; set; }
        public DateTime TestEndTime { get; set; }
        
        // Brake Force Values (Total)
        public double MaxBrakeForce { get; set; }
        public string ValidationStatus { get; set; } = "Pending"; // Pass/Fail
        
        // Raw Data (Optional, for detailed logging)
        public int SampleCount { get; set; }
    }
}
