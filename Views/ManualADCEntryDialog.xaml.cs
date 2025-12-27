using System;
using System.Windows;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class ManualADCEntryDialog : Window
    {
        public ushort InternalADC { get; private set; }
        public int ADS1115ADC { get; private set; }  // Changed to int for signed support (-32768 to +32767)
        
        public ManualADCEntryDialog(double weight)
        {
            InitializeComponent();
            WeightTxt.Text = $"{weight:F0} kg";
        }
        
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(InternalADCTxt.Text, out int internalADC))
                {
                    if (internalADC < 0 || internalADC > 16380)
                    {
                        MessageBox.Show("Invalid Internal ADC value. Please enter a number between 0 and 16380.", 
                                      "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    InternalADC = (ushort)internalADC;
                }
                else
                {
                    MessageBox.Show("Invalid Internal ADC value. Please enter a valid number.", 
                                  "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (int.TryParse(ADS1115ADCTxt.Text, out int ads1115ADC))
                {
                    if (ads1115ADC < -131072 || ads1115ADC > 131068)
                    {
                        MessageBox.Show("Invalid ADS1115 ADC value. Please enter a signed number between -131072 and +131068.", 
                                      "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    ADS1115ADC = ads1115ADC;
                }
                else
                {
                    MessageBox.Show("Invalid ADS1115 ADC value. Please enter a signed number (e.g., -15, 0, 100).", 
                                  "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

