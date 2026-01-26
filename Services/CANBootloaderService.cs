using System;
using ATS_TwoWheeler_WPF.Core;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;

namespace ATS_TwoWheeler_WPF.Services
{
    public class CANBootloaderService : ICANBootloaderService, IDisposable
    {
        private readonly ICANService _canService;

        // Events extracted from CANService
        public event EventHandler<BootPingResponseEventArgs>? BootPingResponseReceived;
        public event EventHandler<BootBeginResponseEventArgs>? BootBeginResponseReceived;
        public event EventHandler<BootProgressEventArgs>? BootProgressReceived;
        public event EventHandler<BootEndResponseEventArgs>? BootEndResponseReceived;
        public event EventHandler<BootErrorEventArgs>? BootErrorReceived;
        public event EventHandler<BootQueryResponseEventArgs>? BootQueryResponseReceived;

        public CANBootloaderService(ICANService canService)
        {
            _canService = canService;
            _canService.MessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(CANMessage message)
        {
            // Only process if we have data or if it's a known empty-data response type
            // (Some responses like Ping might be empty, but usually have data in this protocol?)
            // The Original DecodeFrame handled empty data for some cases.
            
            uint canId = message.ID;
            byte[] canData = message.Data;

            switch (canId)
            {
                case BootloaderProtocol.CanIdBootPingResponse:
                    BootPingResponseReceived?.Invoke(this, new BootPingResponseEventArgs { Timestamp = DateTime.Now });
                    break;

                case BootloaderProtocol.CanIdBootBeginResponse:
                    if (canData != null && canData.Length >= 1)
                        BootBeginResponseReceived?.Invoke(this, new BootBeginResponseEventArgs 
                        { 
                            Status = (BootloaderStatus)canData[0],
                            Timestamp = DateTime.Now 
                        });
                    break;

                case BootloaderProtocol.CanIdBootProgress:
                    if (canData != null && canData.Length >= 5)
                        BootProgressReceived?.Invoke(this, new BootProgressEventArgs
                        {
                            Percent = canData[0],
                            BytesReceived = BitConverter.ToUInt32(canData, 1),
                            Timestamp = DateTime.Now
                        });
                    break;

                case BootloaderProtocol.CanIdBootEndResponse:
                    if (canData != null && canData.Length >= 1)
                        BootEndResponseReceived?.Invoke(this, new BootEndResponseEventArgs
                        {
                            Status = (BootloaderStatus)canData[0],
                            Timestamp = DateTime.Now
                        });
                    break;

                case BootloaderProtocol.CanIdBootError:
                case BootloaderProtocol.CanIdErrSize:
                case BootloaderProtocol.CanIdErrWrite:
                case BootloaderProtocol.CanIdErrValidation:
                case BootloaderProtocol.CanIdErrBuffer:
                    if (canData != null)
                        BootErrorReceived?.Invoke(this, new BootErrorEventArgs
                        {
                            CanId = canId,
                            RawData = canData,
                            Timestamp = DateTime.Now
                        });
                    break;

                case BootloaderProtocol.CanIdBootQueryResponse:
                    if (canData != null && canData.Length >= 4)
                    {
                        var args = new BootQueryResponseEventArgs
                        {
                            Present = canData[0] == 0x01,
                            Major = canData[1],
                            Minor = canData[2],
                            Patch = canData[3],
                            Timestamp = DateTime.Now
                        };
                        
                        // Extended format (8 bytes) handling
                        if (canData.Length >= 8)
                        {
                            args.ActiveBank = canData[4];
                            args.BankAValid = canData[5];
                            args.BankBValid = canData[6];
                        }
                        else
                        {
                            args.ActiveBank = 0;
                            args.BankAValid = 0x00;
                            args.BankBValid = 0x00;
                        }
                        
                        BootQueryResponseReceived?.Invoke(this, args);
                    }
                    break;
            }
        }

        // Commands - Interface implementation
        public bool QueryBootloaderInfo() => _canService.SendMessage(BootloaderProtocol.CanIdBootQueryInfo, Array.Empty<byte>());
        public bool RequestEnterBootloader() => _canService.SendMessage(BootloaderProtocol.CanIdBootEnter, Array.Empty<byte>());
        public bool RequestReset() => _canService.SendMessage(BootloaderProtocol.CanIdBootReset, Array.Empty<byte>());
        public bool SendPing() => _canService.SendMessage(BootloaderProtocol.CanIdBootPing, Array.Empty<byte>());
        
        public void ProcessBootloaderMessage(uint canId, byte[] data)
        {
            OnMessageReceived(new CANMessage { ID = canId, Data = data });
        }

        public void Dispose()
        {
            _canService.MessageReceived -= OnMessageReceived;
        }
    }
}
