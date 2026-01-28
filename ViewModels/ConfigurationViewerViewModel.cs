using System;
using System.Linq;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class ConfigurationViewerViewModel : BaseViewModel
    {
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialog;

        public string InternalCalFileLocation => _settings.GetCalibrationFilePath(false);
        public string InternalCalStatus => _settings.CalibrationDataInternal.IsValid ? "Calibrated" : "Not Calibrated";
        public double InternalCalSlope => _settings.CalibrationDataInternal.Slope;
        public double InternalCalIntercept => _settings.CalibrationDataInternal.Intercept;
        public string InternalCalDate => _settings.CalibrationDataInternal.CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int InternalCalZeroPoint => _settings.CalibrationDataInternal.Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int InternalCalKnownWeightAdc => _settings.CalibrationDataInternal.Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string AdsCalFileLocation => _settings.GetCalibrationFilePath(true);
        public string AdsCalStatus => _settings.CalibrationDataADS1115.IsValid ? "Calibrated" : "Not Calibrated";
        public double AdsCalSlope => _settings.CalibrationDataADS1115.Slope;
        public double AdsCalIntercept => _settings.CalibrationDataADS1115.Intercept;
        public string AdsCalDate => _settings.CalibrationDataADS1115.CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int AdsCalZeroPoint => _settings.CalibrationDataADS1115.Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int AdsCalKnownWeightAdc => _settings.CalibrationDataADS1115.Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string TareFileLocation => _settings.GetTareFilePath();
        public string TareStatus => _settings.TareValue != 0 ? "Active" : "Stable/Zero";
        public double TareBaseline => _settings.TareValue;

        public string DataDirectoryPath => PathHelper.GetDataDirectory();

        public ICommand ResetInternalCommand { get; }
        public ICommand ResetAdsCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand RefreshCommand { get; }

        public ConfigurationViewerViewModel(ISettingsService settings, IDialogService dialog)
        {
            _settings = settings;
            _dialog = dialog;

            ResetInternalCommand = new RelayCommand(_ => ResetInternal());
            ResetAdsCommand = new RelayCommand(_ => ResetAds());
            OpenDataDirectoryCommand = new RelayCommand(_ => OpenDataDirectory());
            RefreshCommand = new RelayCommand(_ => Refresh());
        }

        private void ResetInternal()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset Internal Calibration?", "Reset Configuration"))
            {
                _settings.ResetCalibration(false);
                Refresh();
            }
        }

        private void ResetAds()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset ADS1115 Calibration?", "Reset Configuration"))
            {
                _settings.ResetCalibration(true);
                Refresh();
            }
        }

        private void OpenDataDirectory()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", DataDirectoryPath);
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"Could not open directory: {ex.Message}", "Error");
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(""); // Refresh all properties
        }
    }
}
