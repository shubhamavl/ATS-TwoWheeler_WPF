using System;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface INavigationService
    {
        void ShowBootloaderManager();
        void ShowTwoWheelerWindow();
        void ShowCalibrationDialog(bool isBrakeMode = false);
        void ShowMonitorWindow();
        void ShowLogsWindow();
        void ShowStatusHistory();
        void CloseWindow(object window);
    }
}
