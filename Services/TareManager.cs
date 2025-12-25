using System;
using System.IO;
using System.Text.Json;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    /// <summary>
    /// Tare manager for calibrator weight offset compensation (ATS Two-Wheeler).
    /// When calibrator weight is removed after calibration, the scale shows negative weight.
    /// Tare stores this negative offset and adds it to all future readings to compensate.
    /// Supports mode-specific offsets: Internal/ADS1115 (2 independent offsets)
    /// </summary>
    public class TareManager
    {
        // Total weight offsets (negative values from calibrator removal)
        public double TotalOffsetKgInternal { get; private set; }
        public double TotalOffsetKgADS1115 { get; private set; }
        public bool TotalIsTaredInternal { get; private set; }
        public bool TotalIsTaredADS1115 { get; private set; }
        public DateTime TotalTareTimeInternal { get; private set; }
        public DateTime TotalTareTimeADS1115 { get; private set; }
        
        /// <summary>
        /// Tare total weight: store positive offset from calibrator removal for compensation
        /// When calibrator is removed, weight goes negative (e.g., -23 kg).
        /// Tare stores the absolute value (e.g., +23 kg) to compensate all future readings.
        /// </summary>
        /// <param name="currentCalibratedKg">Current calibrated weight in kg (must be negative)</param>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        public void TareTotal(double currentCalibratedKg, byte adcMode)
        {
            // Validate offset value
            if (double.IsNaN(currentCalibratedKg) || double.IsInfinity(currentCalibratedKg))
            {
                throw new ArgumentException($"Invalid tare offset: {currentCalibratedKg} (NaN or Infinity)", nameof(currentCalibratedKg));
            }
            
            // Only allow negative values (calibrator removed scenario)
            if (currentCalibratedKg >= 0)
            {
                throw new ArgumentException($"Tare only works with negative weight (calibrator removed). Current weight: {currentCalibratedKg:F3} kg", nameof(currentCalibratedKg));
            }
            
            // Store the absolute value (positive) as offset for compensation
            double offset = Math.Abs(currentCalibratedKg);
            
            if (adcMode == 0) // Internal
            {
                TotalOffsetKgInternal = offset; // Store positive offset
                TotalIsTaredInternal = true;
                TotalTareTimeInternal = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"[TareManager] Total Internal tare set: offset=+{offset:F3} kg (from {currentCalibratedKg:F3} kg)");
            }
            else // ADS1115
            {
                TotalOffsetKgADS1115 = offset; // Store positive offset
                TotalIsTaredADS1115 = true;
                TotalTareTimeADS1115 = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"[TareManager] Total ADS1115 tare set: offset=+{offset:F3} kg (from {currentCalibratedKg:F3} kg)");
            }
        }
        
        /// <summary>
        /// Reset total tare for specific ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        public void ResetTotal(byte adcMode)
        {
            if (adcMode == 0) // Internal
            {
                TotalIsTaredInternal = false;
                TotalOffsetKgInternal = 0;
            }
            else // ADS1115
            {
                TotalIsTaredADS1115 = false;
                TotalOffsetKgADS1115 = 0;
            }
        }
        
        /// <summary>
        /// Reset all tares (all modes)
        /// </summary>
        public void ResetAll()
        {
            ResetTotal(0);
            ResetTotal(1);
        }
        
        /// <summary>
        /// Apply tare: add positive offset to current calibrated weight to compensate for calibrator removal
        /// Example: If offset is +23 kg and calibrated weight is -23 kg, result = 0 kg
        /// If calibrated weight is -13 kg (added 10 kg), result = -13 + 23 = 10 kg
        /// </summary>
        /// <param name="calibratedKg">Current calibrated weight in kg</param>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Compensated weight (calibrated + offset, where offset is positive)</returns>
        public double ApplyTare(double calibratedKg, byte adcMode)
        {
            double offset = 0;
            bool isTared = false;
            
            if (adcMode == 0) // Internal
            {
                offset = TotalOffsetKgInternal;
                isTared = TotalIsTaredInternal;
            }
            else // ADS1115
            {
                offset = TotalOffsetKgADS1115;
                isTared = TotalIsTaredADS1115;
            }
            
            if (isTared)
            {
                // Add positive offset to compensate for calibrator removal
                // Offset is stored as positive value (absolute of negative weight)
                double compensatedWeight = calibratedKg + offset;
                System.Diagnostics.Debug.WriteLine($"[TareManager] ApplyTare: mode={(adcMode == 0 ? "Internal" : "ADS1115")}, calibrated={calibratedKg:F3} kg, offset=+{offset:F3} kg, result={compensatedWeight:F3} kg");
                return compensatedWeight;
            }
            else
            {
                return calibratedKg; // Not tared, return as-is
            }
        }
        
        /// <summary>
        /// Check if total weight and ADC mode is tared
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>True if tared</returns>
        public bool IsTared(byte adcMode)
        {
            return adcMode == 0 ? TotalIsTaredInternal : TotalIsTaredADS1115;
        }
        
        /// <summary>
        /// Get offset weight for ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Offset weight in kg (positive value, stored as absolute of negative weight)</returns>
        public double GetOffsetKg(byte adcMode)
        {
            return adcMode == 0 ? TotalOffsetKgInternal : TotalOffsetKgADS1115;
        }
        
        /// <summary>
        /// Get tare time for ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Tare time or DateTime.MinValue if not tared</returns>
        public DateTime GetTareTime(byte adcMode)
        {
            return adcMode == 0 ? TotalTareTimeInternal : TotalTareTimeADS1115;
        }
        
        /// <summary>
        /// Get tare status text for display
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Status text</returns>
        public string GetTareStatusText(byte adcMode)
        {
            bool isTared = IsTared(adcMode);
            if (isTared)
            {
                double offset = GetOffsetKg(adcMode);
                return $"✓ Tared (offset: {offset:F0}kg)";
            }
            else
            {
                return "- Not Tared";
            }
        }
        
        /// <summary>
        /// Get tare time for display
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Tare time or empty string if not tared</returns>
        public string GetTareTimeText(byte adcMode)
        {
            if (IsTared(adcMode))
            {
                DateTime tareTime = GetTareTime(adcMode);
                return tareTime.ToString("HH:mm:ss");
            }
            else
            {
                return "";
            }
        }
        
        /// <summary>
        /// Save tare state to JSON file
        /// </summary>
        public void SaveToFile()
        {
            var tareData = new TareData
            {
                // Total weight offsets
                TotalOffsetKgInternal = TotalOffsetKgInternal,
                TotalOffsetKgADS1115 = TotalOffsetKgADS1115,
                TotalIsTaredInternal = TotalIsTaredInternal,
                TotalIsTaredADS1115 = TotalIsTaredADS1115,
                TotalTareTimeInternal = TotalTareTimeInternal,
                TotalTareTimeADS1115 = TotalTareTimeADS1115,
                
                SaveTime = DateTime.Now
            };
            
            string path = PathHelper.GetTareConfigPath(); // Portable: in Data directory
            string jsonString = JsonSerializer.Serialize(tareData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, jsonString);
        }
        
        /// <summary>
        /// Load tare state from JSON file
        /// </summary>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFromFile()
        {
            string path = PathHelper.GetTareConfigPath(); // Portable: in Data directory
            if (!File.Exists(path))
                return false;
                
            try
            {
                string jsonString = File.ReadAllText(path);
                var tareData = JsonSerializer.Deserialize<TareData>(jsonString);
                
                if (tareData != null)
                {
                    // Load offsets
                    TotalOffsetKgInternal = tareData.TotalOffsetKgInternal;
                    TotalOffsetKgADS1115 = tareData.TotalOffsetKgADS1115;
                    TotalIsTaredInternal = tareData.TotalIsTaredInternal;
                    TotalIsTaredADS1115 = tareData.TotalIsTaredADS1115;
                    TotalTareTimeInternal = tareData.TotalTareTimeInternal;
                    TotalTareTimeADS1115 = tareData.TotalTareTimeADS1115;
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tare config: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Delete tare configuration file
        /// </summary>
        public void DeleteConfig()
        {
            string path = PathHelper.GetTareConfigPath(); // Portable: in Data directory
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting tare config: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Data structure for tare configuration persistence (offset-based)
    /// </summary>
    public class TareData
    {
        // Mode-specific offsets for total weight
        public double TotalOffsetKgInternal { get; set; }
        public double TotalOffsetKgADS1115 { get; set; }
        public bool TotalIsTaredInternal { get; set; }
        public bool TotalIsTaredADS1115 { get; set; }
        public DateTime TotalTareTimeInternal { get; set; }
        public DateTime TotalTareTimeADS1115 { get; set; }
        
        public DateTime SaveTime { get; set; }
    }
}
