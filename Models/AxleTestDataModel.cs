using System;
using System.Collections.Generic;
using System.Linq;

namespace ATS_TwoWheeler_WPF.Models
{
    /// <summary>
    /// Model for storing axle weight test data for a single axle.
    /// </summary>
    public class AxleTestDataModel
    {
        /// <summary>
        /// Unique test identifier (GUID or timestamp-based)
        /// </summary>
        public string TestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Axle number (1, 2, 3, etc.)
        /// </summary>
        public int AxleNumber { get; set; } = 1;

        /// <summary>
        /// Test start time
        /// </summary>
        public DateTime TestStartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Test end time (when saved)
        /// </summary>
        public DateTime? TestEndTime { get; set; }

        /// <summary>
        /// Total weight (kg) - sum of all 4 load cells
        /// </summary>
        public double TotalWeight { get; set; } = 0.0;

        /// <summary>
        /// Minimum weight observed during test (kg)
        /// </summary>
        public double MinWeight { get; set; } = double.MaxValue;

        /// <summary>
        /// Maximum weight observed during test (kg)
        /// </summary>
        public double MaxWeight { get; set; } = double.MinValue;

        /// <summary>
        /// Number of samples collected during test
        /// </summary>
        public int SampleCount { get; set; } = 0;

        /// <summary>
        /// Validation status: "Pass" if >= 10kg, "Fail" if < 10kg
        /// </summary>
        public string ValidationStatus { get; set; } = "Not Tested";

        /// <summary>
        /// Test duration in seconds
        /// </summary>
        public double TestDurationSeconds => TestEndTime.HasValue
            ? (TestEndTime.Value - TestStartTime).TotalSeconds
            : (DateTime.Now - TestStartTime).TotalSeconds;

    }

    /// <summary>
    /// Model for storing complete axle test session data (all axles).
    /// </summary>
    public class AxleTestSessionModel
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Session start time
        /// </summary>
        public DateTime SessionStartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Session end time
        /// </summary>
        public DateTime? SessionEndTime { get; set; }

        /// <summary>
        /// List of axle test data (one per axle)
        /// </summary>
        public List<AxleTestDataModel> AxleTests { get; set; } = new List<AxleTestDataModel>();

        /// <summary>
        /// Total number of axles tested
        /// </summary>
        public int TotalAxles => AxleTests.Count;

        /// <summary>
        /// Total weight across all axles
        /// </summary>
        public double TotalWeight => AxleTests.Sum(a => a.TotalWeight);
    }
}
