using System.Windows;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class SettingsInfoDialog : Window
    {
        public SettingsInfoDialog(string title, string content)
        {
            InitializeComponent();
            TitleTxt.Text = title;
            ContentTxt.Text = content;
        }
    }
}
