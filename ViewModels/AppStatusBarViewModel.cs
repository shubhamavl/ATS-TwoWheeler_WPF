using System;
using System.Windows.Input;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class AppStatusBarViewModel : BaseViewModel
    {
        private readonly ICANService _canService;
        private readonly IUpdateService _updateService;
        private readonly IDialogService _dialogService;

        private string _statusText = "Ready | CAN v0.9 @ 250 kbps";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _streamStatusText = "Idle";
        public string StreamStatusText
        {
            get => _streamStatusText;
            set => SetProperty(ref _streamStatusText, value);
        }

        private long _txCount;
        public long TxCount
        {
            get => _txCount;
            set => SetProperty(ref _txCount, value);
        }

        private long _rxCount;
        public long RxCount
        {
            get => _rxCount;
            set => SetProperty(ref _rxCount, value);
        }

        private string _timestampText = "";
        public string TimestampText
        {
            get => _timestampText;
            set => SetProperty(ref _timestampText, value);
        }

        private string _downloadStatus = "";
        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                if (SetProperty(ref _downloadStatus, value))
                {
                    OnPropertyChanged(nameof(IsDownloadVisible));
                }
            }
        }

        public bool IsDownloadVisible => !string.IsNullOrEmpty(DownloadStatus);

        public ICommand CheckForUpdatesCommand { get; }

        public AppStatusBarViewModel(ICANService canService, IUpdateService updateService, IDialogService dialogService)
        {
            _canService = canService;
            _updateService = updateService;
            _dialogService = dialogService;

            CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync());
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                DownloadStatus = "Checking for updates...";
                var result = await _updateService.CheckForUpdateAsync();

                if (result.IsSuccess && result.Info != null)
                {
                    if (result.Info.IsUpdateAvailable)
                    {
                        bool confirm = _dialogService.ShowConfirmation(
                            $"New version {result.Info.LatestVersion} is available. (Current: {result.Info.CurrentVersion})\n\n" +
                            $"Release Notes:\n{result.Info.ReleaseNotes}\n\n" +
                            "Do you want to download it now?",
                            "Update Available");

                        if (confirm)
                        {
                            await DownloadUpdateAsync(result.Info);
                        }
                        else
                        {
                            DownloadStatus = "Update available";
                        }
                    }
                    else
                    {
                        DownloadStatus = "";
                        _dialogService.ShowMessage("You are running the latest version.", "System Update");
                    }
                }
                else
                {
                    DownloadStatus = "Update check failed";
                    _dialogService.ShowError($"Update check failed: {result.ErrorMessage}", "Update Error");
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = "Error";
                _dialogService.ShowError($"Error checking for updates: {ex.Message}", "System Error");
            }
        }

        private async Task DownloadUpdateAsync(UpdateService.UpdateInfo info)
        {
            try
            {
                var progress = new Progress<double>(p =>
                {
                    DownloadStatus = $"Downloading: {p:P0}";
                });

                var result = await _updateService.DownloadUpdateAsync(info, progress);

                if (result.IsSuccess)
                {
                    DownloadStatus = "Download complete";
                    _dialogService.ShowMessage($"Update downloaded to: {result.FilePath}\n\nPlease install it manually to finish.", "Update Downloaded");
                }
                else
                {
                    DownloadStatus = "Download failed";
                    _dialogService.ShowError($"Download failed: {result.ErrorMessage}", "Download Error");
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = "Download error";
                _dialogService.ShowError($"Error downloading update: {ex.Message}", "System Error");
            }
        }

        public void Refresh()
        {
            if (_canService != null)
            {
                TxCount = _canService.TxMessageCount;
                RxCount = _canService.RxMessageCount;
            }
            TimestampText = DateTime.Now.ToString("HH:mm:ss");
        }

        public void UpdateStreamStatus(bool isStreaming)
        {
            StreamStatusText = isStreaming ? "Streaming..." : "Idle";
        }
    }
}
