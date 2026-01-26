using System.Windows.Controls;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.ViewModels;

namespace ATS_TwoWheeler_WPF.Views.Controls
{
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
            
            // Set DataContext to SettingsViewModel using SettingsManager singleton
            DataContext = new SettingsViewModel(SettingsManager.Instance);
        }
    }
}
