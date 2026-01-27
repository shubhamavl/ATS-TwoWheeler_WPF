using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ATS_TwoWheeler_WPF.Adapters;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels.Base;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels
{
    public class ConnectionViewModel : BaseViewModel
    {
        private readonly ICANService _canService;
        private readonly ISettingsService _settings;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionButtonText));
                    OnPropertyChanged(nameof(ConnectionButtonColor));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StartStreamCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionButtonText => IsConnected ? "❌ Disconnect" : "🔌 Connect";
        public Brush ConnectionButtonColor => IsConnected 
            ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) // Red
            : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green

        public Brush StatusColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(40, 167, 69)) // Green
            : new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

        public string ConnectionStatusText => IsConnected ? "Connected" : "Disconnected";

        private bool _isStreaming;
        public bool IsStreaming
        {
            get => _canService.IsStreaming;
            set
            {
                if (SetProperty(ref _isStreaming, value))
                {
                    ((RelayCommand)StartStreamCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StopStreamCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Adapter Configuration
        public ObservableCollection<string> AdapterTypes { get; } = new ObservableCollection<string> { "USB-CAN-A Serial", "PCAN" };
        
        private string _selectedAdapterType = "USB-CAN-A Serial";
        public string SelectedAdapterType
        {
            get => _selectedAdapterType;
            set
            {
                if (SetProperty(ref _selectedAdapterType, value))
                {
                    IsUsbAdapter = value == "USB-CAN-A Serial";
                    IsPcanAdapter = value == "PCAN";
                }
            }
        }

        private bool _isUsbAdapter;
        public bool IsUsbAdapter
        {
            get => _isUsbAdapter;
            set => SetProperty(ref _isUsbAdapter, value);
        }

        private bool _isPcanAdapter;
        public bool IsPcanAdapter
        {
            get => _isPcanAdapter;
            set => SetProperty(ref _isPcanAdapter, value);
        }

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();
        
        private string _selectedPort = "";
        public string SelectedPort
        {
            get => _selectedPort;
            set => SetProperty(ref _selectedPort, value);
        }

        public ObservableCollection<string> BaudRates { get; } = new ObservableCollection<string> 
        { 
            "125 kbps", "250 kbps", "500 kbps", "1 Mbps" 
        };

        private string _selectedBaudRate = "250 kbps";
        public string SelectedBaudRate
        {
            get => _selectedBaudRate;
            set => SetProperty(ref _selectedBaudRate, value);
        }

        public ObservableCollection<string> StreamingRates { get; } = new ObservableCollection<string>
        {
            "1 Hz", "100 Hz", "500 Hz", "1 kHz"
        };

        private string _selectedStreamingRate = "1 kHz";
        public string SelectedStreamingRate
        {
            get => _selectedStreamingRate;
            set => SetProperty(ref _selectedStreamingRate, value);
        }

        public ICommand ConnectCommand { get; }
        public ICommand StartStreamCommand { get; }
        public ICommand StopStreamCommand { get; }
        public ICommand RefreshPortsCommand { get; }

        public ConnectionViewModel(ICANService canService, ISettingsService settings)
        {
            _canService = canService;
            _settings = settings;

            ConnectCommand = new RelayCommand(OnConnect);
            StartStreamCommand = new RelayCommand(OnStartStream, _ => IsConnected && !IsStreaming);
            StopStreamCommand = new RelayCommand(OnStopStream, _ => IsConnected); // Stop all always active if connected
            RefreshPortsCommand = new RelayCommand(OnRefreshPorts);

            // Initialize from Settings
            LoadSettings();
            RefreshPorts();
        }

        private void LoadSettings()
        {
            SelectedAdapterType = "USB-CAN-A Serial"; 
            IsUsbAdapter = SelectedAdapterType == "USB-CAN-A Serial";
            IsPcanAdapter = SelectedAdapterType == "PCAN";
            
            SelectedPort = _settings.Settings.ComPort;
            SelectedBaudRate = GetBaudRateString(_settings.Settings.CanBaudRate);
            SelectedStreamingRate = GetStreamingRateString(_settings.Settings.TransmissionRate);
        }

        private void OnRefreshPorts(object? obj)
        {
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            AvailablePorts.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                AvailablePorts.Add(port);
            }
            
            // If the currently selected port is not in the list, or is empty, try to pick the first available
            if (!string.IsNullOrEmpty(SelectedPort) && !AvailablePorts.Contains(SelectedPort))
            {
                if (AvailablePorts.Count > 0)
                {
                    SelectedPort = AvailablePorts[0];
                }
                else
                {
                    SelectedPort = string.Empty;
                }
            }
            else if (AvailablePorts.Count > 0 && string.IsNullOrEmpty(SelectedPort))
            {
                SelectedPort = AvailablePorts[0];
            }
        }

        private void OnConnect(object? parameter)
        {
            if (IsConnected)
            {
                _canService.Disconnect();
                IsConnected = false;
                IsStreaming = false;
            }
            else
            {
                // Create config based on selection
                CanAdapterConfig? config = null;

                if (IsUsbAdapter)
                {
                    if (string.IsNullOrEmpty(SelectedPort))
                    {
                        MessageBox.Show("Please select a COM port.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    ushort baudRate = GetBaudRateValue(SelectedBaudRate);
                    config = new UsbSerialCanAdapterConfig
                    {
                        PortName = SelectedPort,
                        SerialBaudRate = 2000000, 
                        BitrateKbps = baudRate
                    };
                    
                    // Update and save settings
                    _settings.SetComPort(SelectedPort);
                    _settings.SetCanBaudRate(SelectedBaudRate);
                    _settings.SaveSettings();
                }
                else if (IsPcanAdapter)
                {
                   // Placeholder for PCAN config logic if implemented
                   MessageBox.Show("PCAN support not fully implemented in this refactor view.", "Info");
                   return;
                }

                if (config != null)
                {
                    if (_canService.Connect(config, out string error))
                    {
                        IsConnected = true;
                    }
                    else
                    {
                        MessageBox.Show($"Connection failed: {error}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void OnStartStream(object? parameter)
        {
            byte rate = GetStreamingRateValue(SelectedStreamingRate);
            if (_canService.StartStream(rate)) 
            {
                IsStreaming = true;
                _settings.SetTransmissionRate(SelectedStreamingRate);
                _settings.SaveSettings();
            }
        }

        private byte GetStreamingRateValue(string rateString)
        {
             return rateString switch
             {
                 "1 Hz" => 0x05,
                 "100 Hz" => 0x01,
                 "500 Hz" => 0x02,
                 "1 kHz" => 0x03,
                 _ => 0x03
             };
        }

        private void OnStopStream(object? parameter)
        {
            _canService.StopAllStreams();
            IsStreaming = false;
        }

        private ushort GetBaudRateValue(string rateString)
        {
             return rateString switch
             {
                 "125 kbps" => 125,
                 "250 kbps" => 250,
                 "500 kbps" => 500,
                 "1 Mbps" => 1000,
                 _ => 250
             };
        }
        private string GetBaudRateString(byte rate)
        {
             return rate switch
             {
                 0x00 => "125 kbps",
                 0x01 => "250 kbps",
                 0x02 => "500 kbps",
                 0x03 => "1 Mbps",
                 _ => "250 kbps"
             };
        }

        private string GetStreamingRateString(byte rate)
        {
             return rate switch
             {
                 0x05 => "1 Hz",
                 0x01 => "100 Hz",
                 0x02 => "500 Hz",
                 0x03 => "1 kHz",
                 _ => "1 kHz"
             };
        }
    }
}
