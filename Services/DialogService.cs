using System.Windows;
using Microsoft.Win32;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public bool ShowConfirmation(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string? ShowSaveFileDialog(string filter, string defaultName, string title = "Save File")
        {
            var saveDialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultName
            };

            return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
        }

        public string? ShowOpenFileDialog(string filter, string title = "Open File")
        {
            var openDialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };

            return openDialog.ShowDialog() == true ? openDialog.FileName : null;
        }
    }
}
