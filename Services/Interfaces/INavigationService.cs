using System;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface INavigationService
    {
        void ShowTwoWheelerWindow();
        void ShowCalibrationDialog();
        void ShowMonitorWindow();
        void ShowLogsWindow();
        void ShowSettingsInfo();
        void CloseWindow(object window);
    }
}
