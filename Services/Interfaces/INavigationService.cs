using System;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface INavigationService
    {
        void ShowBootloaderManager();
        void ShowTwoWheelerWindow();
        void ShowCalibrationDialog();
        void ShowMonitorWindow();
        void ShowLogsWindow();

        void CloseWindow(object window);
    }
}
