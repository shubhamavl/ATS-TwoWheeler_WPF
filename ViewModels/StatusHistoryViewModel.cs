using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class StatusHistoryViewModel : BaseViewModel
    {
        private readonly StatusHistoryManager _historyManager;

        public ObservableCollection<StatusHistoryEntry> StatusEntries { get; }

        public ICommand ClearHistoryCommand { get; }

        public StatusHistoryViewModel(StatusHistoryManager historyManager)
        {
            _historyManager = historyManager;
            StatusEntries = new ObservableCollection<StatusHistoryEntry>(_historyManager.GetAllEntries());
            ClearHistoryCommand = new RelayCommand(OnClearHistory);

            // Note: If we want real-time updates while window is open, we'd need an event from manager.
            // For now, it loads on open.
        }

        private void OnClearHistory(object? parameter)
        {
            _historyManager.ClearHistory();
            StatusEntries.Clear();
        }
    }
}
