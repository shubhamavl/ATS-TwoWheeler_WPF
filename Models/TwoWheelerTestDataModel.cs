// SPDX-License-Identifier: Apache-2.0
// Copyright Pionix GmbH and Contributors to EVerest

using System;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Model for two-wheeler weight test data (simplified for total weight measurement)
    /// </summary>
    public class TwoWheelerTestDataModel
    {
        public string TestId { get; set; } = string.Empty;
        public DateTime TestStartTime { get; set; }
        public DateTime TestEndTime { get; set; }
        public double InitialWeight { get; set; } // Initial weight when test started (for Y-axis scaling)
        
        // Weight data
        public double MinWeight { get; set; } // Minimum weight during test
        public double MaxWeight { get; set; } // Maximum weight during test
        public double TotalWeight { get; set; } // Total weight (sum of all 4 channels)
        public string TestResult { get; set; } = "Not Tested"; // "Pass", "Fail", or "Not Tested"
        
        // Common settings
        public string Limits { get; set; } = ""; // Limit string (e.g., "≥10.0 kg")
        public int SampleCount { get; set; } // Number of data points collected
        public string TransmissionRate { get; set; } = "1kHz"; // CAN transmission rate
        public double[]? DataPoints { get; set; } // Full data array (optional, can be large)
    }
}

