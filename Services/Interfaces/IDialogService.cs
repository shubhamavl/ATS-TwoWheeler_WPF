namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        void ShowError(string message, string title);
        void ShowWarning(string message, string title);
        bool ShowConfirmation(string message, string title);
        string? ShowSaveFileDialog(string filter, string defaultName, string title = "Save File");
        string? ShowOpenFileDialog(string filter, string title = "Open File");
    }
}
