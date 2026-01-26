using System;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Services;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class AppStatusBarViewModel : BaseViewModel
    {
        private readonly ICANService _canService;

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

        public AppStatusBarViewModel(ICANService canService)
        {
            _canService = canService;
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
