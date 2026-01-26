using System;
using System.Collections.Generic;
using ATS_TwoWheeler_WPF.Core;
using Xunit;

namespace ATS_TwoWheeler_WPF.Tests.Core
{
    public class LinearCalibrationTests
    {
        [Fact]
        public void FitMultiplePoints_SinglePoint_CalculatesCorrectSlope()
        {
            // Arrange
            var points = new List<CalibrationPoint>
            {
                new CalibrationPoint { RawADC = 1000, KnownWeight = 10.0 }
            };

            // Act
            var calibration = LinearCalibration.FitMultiplePoints(points);

            // Assert
            Assert.Equal(0.01, calibration.Slope, 5); // 10 / 1000 = 0.01
            Assert.Equal(0, calibration.Intercept);
            Assert.True(calibration.IsValid);
            Assert.Equal(1.0, calibration.R2);
        }

        [Fact]
        public void FitMultiplePoints_PerfectLinearData_CalculatesCorrectCoefficients()
        {
            // Arrange: y = 2x + 10
            var points = new List<CalibrationPoint>
            {
                new CalibrationPoint { RawADC = 0, KnownWeight = 10.0 },
                new CalibrationPoint { RawADC = 10, KnownWeight = 30.0 },
                new CalibrationPoint { RawADC = 20, KnownWeight = 50.0 }
            };

            // Act
            var calibration = LinearCalibration.FitMultiplePoints(points);

            // Assert
            Assert.Equal(2.0, calibration.Slope, 5);
            Assert.Equal(10.0, calibration.Intercept, 5);
            Assert.Equal(1.0, calibration.R2, 5);
        }

        [Fact]
        public void FitMultiplePoints_NoisyData_CalculatesRealisticR2()
        {
            // Arrange
            var points = new List<CalibrationPoint>
            {
                new CalibrationPoint { RawADC = 10, KnownWeight = 21.0 },
                new CalibrationPoint { RawADC = 20, KnownWeight = 39.0 },
                new CalibrationPoint { RawADC = 30, KnownWeight = 62.0 }
            };

            // Act
            var calibration = LinearCalibration.FitMultiplePoints(points);

            // Assert
            Assert.True(calibration.R2 < 1.0);
            Assert.True(calibration.R2 > 0.9);
            Assert.True(calibration.MaxErrorPercent > 0);
        }

        [Fact]
        public void FitMultiplePoints_DuplicatePoints_ThrowsArgumentException()
        {
            // Arrange
            var points = new List<CalibrationPoint>
            {
                new CalibrationPoint { RawADC = 1000, KnownWeight = 10.0 },
                new CalibrationPoint { RawADC = 1000, KnownWeight = 10.0 }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => LinearCalibration.FitMultiplePoints(points));
        }

        [Fact]
        public void FitMultiplePoints_AllZeroADC_ThrowsArgumentException()
        {
            // Arrange
            var points = new List<CalibrationPoint>
            {
                new CalibrationPoint { RawADC = 0, KnownWeight = 0.0 },
                new CalibrationPoint { RawADC = 0, KnownWeight = 10.0 }
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => LinearCalibration.FitMultiplePoints(points));
        }

        [Fact]
        public void RawToKgPiecewise_InterpolatesBetweenPoints()
        {
            // Arrange
            var calibration = new LinearCalibration
            {
                Mode = CalibrationMode.Piecewise,
                IsValid = true,
                Points = new List<CalibrationPoint>
                {
                    new CalibrationPoint { RawADC = 0, KnownWeight = 0.0 },
                    new CalibrationPoint { RawADC = 1000, KnownWeight = 10.0 },
                    new CalibrationPoint { RawADC = 2000, KnownWeight = 30.0 }
                }
            };
            calibration.BuildSegmentsFromPoints();

            // Act
            double weightAt500 = calibration.RawToKg(500);
            double weightAt1500 = calibration.RawToKg(1500);

            // Assert
            Assert.Equal(5.0, weightAt500, 5);  // Midpoint between (0,0) and (1000,10)
            Assert.Equal(20.0, weightAt1500, 5); // Midpoint between (1000,10) and (2000,30)
        }

        [Fact]
        public void RawToKgPiecewise_HandlesExtrapolation()
        {
            // Arrange
            var calibration = new LinearCalibration
            {
                Mode = CalibrationMode.Piecewise,
                IsValid = true,
                Points = new List<CalibrationPoint>
                {
                    new CalibrationPoint { RawADC = 1000, KnownWeight = 10.0 },
                    new CalibrationPoint { RawADC = 2000, KnownWeight = 20.0 }
                }
            };
            calibration.BuildSegmentsFromPoints();

            // Act
            double weightAt500 = calibration.RawToKg(500); // Should use first segment (slope 0.01)
            double weightAt2500 = calibration.RawToKg(2500); // Should use last segment (slope 0.01)

            // Assert
            Assert.Equal(5.0, weightAt500, 5);
            Assert.Equal(25.0, weightAt2500, 5);
        }

        [Fact]
        public void GetQualityAssessment_ReturnsCorrectString()
        {
            // Arrange
            var cal1 = new LinearCalibration { R2 = 0.9999 };
            var cal2 = new LinearCalibration { R2 = 0.995 };
            var cal3 = new LinearCalibration { R2 = 0.96 };
            var cal4 = new LinearCalibration { R2 = 0.90 };

            // Assert
            Assert.Equal("Excellent", cal1.GetQualityAssessment());
            Assert.Equal("Good", cal2.GetQualityAssessment());
            Assert.Equal("Acceptable", cal3.GetQualityAssessment());
            Assert.Equal("Poor", cal4.GetQualityAssessment());
        }
    }
}
