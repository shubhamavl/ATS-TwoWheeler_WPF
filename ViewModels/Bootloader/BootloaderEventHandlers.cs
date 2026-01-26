using System;
using System.Windows;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.ViewModels.Bootloader
{
    /// <summary>
    /// Handles bootloader-related events for the BootloaderViewModel.
    /// Improves modularity by separating event logic from UI coordination.
    /// </summary>
    public class BootloaderEventHandlers : IDisposable
    {
        private readonly BootloaderViewModel _viewModel;
        private readonly ICANBootloaderService _bootloaderService;
        private readonly BootloaderDiagnosticsViewModel _diagnostics;
        private readonly IBootloaderStateMachine _stateMachine;

        public BootloaderEventHandlers(
            BootloaderViewModel viewModel,
            ICANBootloaderService bootloaderService,
            BootloaderDiagnosticsViewModel diagnostics,
            IBootloaderStateMachine stateMachine)
        {
            _viewModel = viewModel;
            _bootloaderService = bootloaderService;
            _diagnostics = diagnostics;
            _stateMachine = stateMachine;

            // Subscribe to events
            _bootloaderService.BootQueryResponseReceived += OnBootQueryResponseReceived;
            _bootloaderService.BootPingResponseReceived += OnBootPingResponseReceived;
            _bootloaderService.BootBeginResponseReceived += OnBootBeginResponseReceived;
            _bootloaderService.BootProgressReceived += OnBootProgressReceived;
            _bootloaderService.BootEndResponseReceived += OnBootEndResponseReceived;
            _bootloaderService.BootErrorReceived += OnBootErrorReceived;
        }

        private void OnBootQueryResponseReceived(object? sender, BootQueryResponseEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel.BootloaderInfo.IsPresent = e.Present;
                if (e.Present)
                {
                    _viewModel.BootloaderInfo.FirmwareVersion = new Version(e.Major, e.Minor, e.Patch);
                    _viewModel.BootloaderInfo.Status = BootloaderStatus.Ready;
                    _viewModel.BootloaderInfo.Bank.IsValid = (e.BankAValid == 0xFF);

                    _diagnostics.LogOperation("Query Response", "RX", BootloaderProtocol.CanIdBootQueryResponse,
                        "Success",
                        $"Version: {e.Major}.{e.Minor}.{e.Patch}, Bank IsValid: {(_viewModel.BootloaderInfo.Bank.IsValid ? "Yes" : "No")}");
                }
                else
                {
                    _diagnostics.LogOperation("Query Response", "RX", BootloaderProtocol.CanIdBootQueryResponse,
                        "Failed", "Bootloader not present");
                }

                _viewModel.UpdateUI();
            });
        }

        private void OnBootPingResponseReceived(object? sender, BootPingResponseEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel.BootloaderInfo.Status = BootloaderStatus.Ready;
                _viewModel.BootloaderInfo.IsPresent = true;

                if (_stateMachine.CurrentStep == BootloaderProcessStep.EnterBootloader)
                {
                    _diagnostics.LogOperation("Enter Bootloader", "SYSTEM", BootloaderProtocol.CanIdBootEnter,
                        "Success", "STM32 entered bootloader mode");
                }

                _viewModel.CurrentStep = BootloaderProcessStep.Ping;
                _diagnostics.LogOperation("Ping Response", "RX", BootloaderProtocol.CanIdBootPingResponse, "Success",
                    "Bootloader ready");
                _viewModel.UpdateUI();
            });
        }

        private void OnBootBeginResponseReceived(object? sender, BootBeginResponseEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Status == BootloaderStatus.InProgress)
                {
                    _viewModel.CurrentStep = BootloaderProcessStep.Transfer;
                    _diagnostics.LogOperation("Begin Response", "RX", BootloaderProtocol.CanIdBootBeginResponse,
                        "Success", $"Status: {e.Status}");
                    _viewModel.StatusText = "Status: Starting firmware transfer...";
                }
                else
                {
                    _viewModel.CurrentStep = BootloaderProcessStep.Failed;
                    _diagnostics.LogOperation("Begin Response", "RX", BootloaderProtocol.CanIdBootBeginResponse,
                        "Failed", $"Status: {e.Status}");
                    _viewModel.StatusText = $"Status: Update failed: {BootloaderProtocol.DescribeStatus(e.Status)}";
                }

                _viewModel.UpdateUI();
            });
        }

        private void OnBootProgressReceived(object? sender, BootProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel.CurrentStep = BootloaderProcessStep.Transfer;
                _viewModel.StatusText = $"Status: Transferring firmware: {e.Percent}% ({e.BytesReceived:N0} bytes)";
                _diagnostics.LogOperation("Progress Update", "RX", BootloaderProtocol.CanIdBootProgress, "In Progress",
                    $"{e.Percent}% - {e.BytesReceived:N0} bytes");
                _viewModel.UpdateDetailedProgress(e.BytesReceived, e.Percent);
            });
        }

        private void OnBootEndResponseReceived(object? sender, BootEndResponseEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Status == BootloaderStatus.Success)
                {
                    _viewModel.CurrentStep = BootloaderProcessStep.Reset;
                    _diagnostics.LogOperation("End Response", "RX", BootloaderProtocol.CanIdBootEndResponse, "Success",
                        "CRC validated, bank switched");
                    _viewModel.StatusText = "Status: Update complete! Resetting...";
                }
                else
                {
                    _viewModel.CurrentStep = BootloaderProcessStep.Failed;
                    _diagnostics.LogOperation("End Response", "RX", BootloaderProtocol.CanIdBootEndResponse, "Failed",
                        $"Status: {BootloaderProtocol.DescribeStatus(e.Status)}");
                    _viewModel.StatusText = $"Status: Update failed: {BootloaderProtocol.DescribeStatus(e.Status)}";
                }

                _viewModel.UpdateUI();
            });
        }

        private void OnBootErrorReceived(object? sender, BootErrorEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _diagnostics.LogOperation("Error", "RX", e.CanId, "Error", e.Message);
                _viewModel.StatusText = $"Status: Error - {e.Message}";
            });
        }

        public void Dispose()
        {
            _bootloaderService.BootQueryResponseReceived -= OnBootQueryResponseReceived;
            _bootloaderService.BootPingResponseReceived -= OnBootPingResponseReceived;
            _bootloaderService.BootBeginResponseReceived -= OnBootBeginResponseReceived;
            _bootloaderService.BootProgressReceived -= OnBootProgressReceived;
            _bootloaderService.BootEndResponseReceived -= OnBootEndResponseReceived;
            _bootloaderService.BootErrorReceived -= OnBootErrorReceived;
        }
    }
}
