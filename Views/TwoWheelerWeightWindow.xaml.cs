using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels;
using Microsoft.Win32;

namespace ATS_TwoWheeler_WPF.Views
{
    public partial class TwoWheelerWeightWindow : Window
    {


        public TwoWheelerWeightWindow(ICANService? canService, IWeightProcessorService? weightProcessor)
        {
            InitializeComponent();

            // UI Update Timer for ViewModel refresh
            DispatcherTimer uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            uiTimer.Tick += (s, e) => (DataContext as TwoWheelerWeightViewModel)?.Refresh();
            uiTimer.Start();

            this.Closed += (s, e) => uiTimer.Stop();
        }


        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as TwoWheelerWeightViewModel;
            if (vm == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.F1:
                    if (vm.StartTestCommand.CanExecute(null))
                    {
                        vm.StartTestCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.F2:
                    if (vm.StopTestCommand.CanExecute(null))
                    {
                        vm.StopTestCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
                case Key.F3:
                    if (vm.SaveTestCommand.CanExecute(null))
                    {
                        vm.SaveTestCommand.Execute(null);
                    }

                    e.Handled = true;
                    break;
            }
        }

    }
}


