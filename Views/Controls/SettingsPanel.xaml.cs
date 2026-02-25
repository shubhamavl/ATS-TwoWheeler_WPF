using System.Windows.Controls;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.ViewModels;
using System.Windows; // Added for DependencyPropertyChangedEventArgs and Window
using ATS_TwoWheeler_WPF.Views; // Fixed namespace

namespace ATS_TwoWheeler_WPF.Views.Controls
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();

            // Subscribe to DataContextChanged to manage HelpRequested event subscription
            this.DataContextChanged += SettingsPanel_DataContextChanged;
            
            // Unsubscribe on Unloaded to prevent leaks and crashes
            this.Unloaded += SettingsPanel_Unloaded;
        }

        private void SettingsPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.HelpRequested -= OnHelpRequested;
            }
        }

        private void SettingsPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SettingsViewModel oldVm)
            {
                oldVm.HelpRequested -= OnHelpRequested;
            }
            if (e.NewValue is SettingsViewModel newVm)
            {
                newVm.HelpRequested += OnHelpRequested;
            }
        }

        private void OnHelpRequested(string title, string content)
        {
            var dialog = new SettingsInfoDialog(title, content)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
    }
}
