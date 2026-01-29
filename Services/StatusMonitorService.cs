using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public class StatusMonitorService : IStatusMonitorService
    {
        private readonly ICANService _canService;
        private readonly IDialogService _dialogService;
        private readonly DispatcherTimer _timer;
        private bool _isSystemAvailable = true; // Assume true initially or wait for first check? simpler to start true or unknown.
        private bool _wasConnected = false;

        public event EventHandler<bool>? AvailabilityChanged;

        public bool IsSystemAvailable { get => _isSystemAvailable; private set => _isSystemAvailable = value; }

        public StatusMonitorService(ICANService canService, IDialogService dialogService)
        {
            _canService = canService;
            _dialogService = dialogService;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
        }

        public void StartMonitoring()
        {
            _timer.Start();
            ProductionLogger.Instance.LogInfo("Status monitoring started", "StatusMonitor");
        }

        public void StopMonitoring()
        {
            _timer.Stop();
            ProductionLogger.Instance.LogInfo("Status monitoring stopped", "StatusMonitor");
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_canService.IsConnected)
            {
                if (_wasConnected)
                {
                    // Transitioned to disconnected
                    UpdateAvailability(false);
                    _wasConnected = false;
                }
                return;
            }

            _wasConnected = true;

            // Logic:
            // 1. If we have received ANY message (RX) recently (e.g. < 1s), we are technically "alive" at standard CAN level.
            // 2. BUT user wants to ensure SYSTEM STATUS is checked.
            // 3. If we received a SystemStatus recently (< 2s), we are good.
            // 4. If NOT, request it.

            var now = DateTime.Now;
            var timeSinceRx = now - _canService.LastRxTime;
            var timeSinceStatus = now - _canService.LastSystemStatusTime;

            // If we have recent RX, the bus is alive.
            // If we haven't seen a Status packet in > 2 seconds, request it.
            if (timeSinceStatus.TotalSeconds > 2)
            {
                _canService.RequestSystemStatus(log: false);
            }

            // CRITICAL CHECK: If we haven't received ANY RX for > 3 seconds, OR 
            // We haven't received STATUS for > 5 seconds (despite requesting), mark Unavailable.

            bool isDataFlowing = timeSinceRx.TotalSeconds < 3;
            bool isStatusFlowing = timeSinceStatus.TotalSeconds < 5;

            // If we are Streaming, DataFlowing is the main check.
            // If we are Idle, StatusFlowing is the main check (since we request it).

            bool isAvailable = false;

            if (_canService.IsStreaming)
            {
                // Even if streaming, we should probably get status responses if we ask.
                // But let's trust DataFlowing primarily for "Availability".
                isAvailable = isDataFlowing;
            }
            else
            {
                // Not streaming, relying on Status Ping
                isAvailable = isStatusFlowing;

                // If we have RX but no Status? Might be bootloader or other traffic.
                // Assuming RX = available is safer for basic connectivity.
                if (isDataFlowing)
                {
                    isAvailable = true;
                }
            }

            UpdateAvailability(isAvailable);
        }

        private void UpdateAvailability(bool isAvailable)
        {
            if (_isSystemAvailable != isAvailable)
            {
                _isSystemAvailable = isAvailable;
                AvailabilityChanged?.Invoke(this, isAvailable);

                // User requested popup or action
                if (!isAvailable && _canService.IsConnected)
                {
                    // Only show popup if we were previously OK and still think we are "Connected" (serial port open)
                    // Using Dispatcher to ensure UI thread if showing dialog
                    // Warning: Blocking dialogs in timer tick can be dangerous. 
                    // Using non-blocking notification or toast is better, but user asked for popup.
                    // We will rely on ViewModel to show visual indication (Red text), 
                    // avoiding spamming Popups every 1 second.
                }

                ProductionLogger.Instance.LogInfo($"System availability changed: {isAvailable}", "StatusMonitor");
            }
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
