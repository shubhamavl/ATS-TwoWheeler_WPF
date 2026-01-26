using System;
using ATS_TwoWheeler_WPF.Adapters;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services;
using ATS_TwoWheeler_WPF.Core;
using Moq;
using Xunit;

namespace ATS_TwoWheeler_WPF.Tests.Services
{
    public class CANServiceTests
    {
        [Fact]
        public void SendMessage_CallsAdapterSendMessage()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var service = new CANService();
            service.SetAdapter(mockAdapter.Object);
            
            // Set connection state manually for testing (it's public volatile)
            service._connected = true;
            
            uint testId = 0x123;
            byte[] testData = new byte[] { 0x01, 0x02 };
            
            mockAdapter.Setup(a => a.SendMessage(testId, testData)).Returns(true);

            // Act
            bool result = service.SendMessage(testId, testData);

            // Assert
            Assert.True(result);
            mockAdapter.Verify(a => a.SendMessage(testId, testData), Times.Once);
        }

        [Fact]
        public void ReceiveRawData_FiresRawDataReceivedEvent()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var service = new CANService();
            service.SetAdapter(mockAdapter.Object);
            
            RawDataEventArgs? receivedArgs = null;
            service.RawDataReceived += (s, e) => receivedArgs = e;
            
            // Simulating internal ADC mode (0)
            service.SetADCMode(0);
            
            // CAN Message with ID 0x200 (Total Raw Data) and 2 bytes of data (Internal ADC)
            var message = new CANMessage(CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA, new byte[] { 0xE8, 0x03 }); // 0x03E8 = 1000

            // Act: Simulate receiving from adapter
            mockAdapter.Raise(a => a.MessageReceived += null, message);

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(1000, receivedArgs.RawADCSum);
        }

        [Fact]
        public void ReceiveSystemStatus_FiresSystemStatusReceivedEvent()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var service = new CANService();
            service.SetAdapter(mockAdapter.Object);
            
            SystemStatusEventArgs? receivedArgs = null;
            service.SystemStatusReceived += (s, e) => receivedArgs = e;
            
            // Minimal System Status (0x300): 3 bytes
            // Byte 0: Packed (Bits 0-1: Status, 2: ADC, 3: Relay) -> 0x04 (ADC=1, others 0)
            // Byte 1: Error Flags -> 0x00
            // Byte 2: Uptime (ignored in minimal check if 3 bytes)
            var message = new CANMessage(CANMessageProcessor.CAN_MSG_ID_SYSTEM_STATUS, new byte[] { 0x04, 0x00, 0x00 });

            // Act
            mockAdapter.Raise(a => a.MessageReceived += null, message);

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(1, receivedArgs.ADCMode);
            Assert.Equal(1, service.CurrentADCMode); // Service state should update
        }

        [Fact]
        public void ReceiveFirmwareVersion_FiresFirmwareVersionReceivedEvent()
        {
            // Arrange
            var mockAdapter = new Mock<ICanAdapter>();
            var service = new CANService();
            service.SetAdapter(mockAdapter.Object);
            
            FirmwareVersionEventArgs? receivedArgs = null;
            service.FirmwareVersionReceived += (s, e) => receivedArgs = e;
            
            // Firmware Version (0x301): 4 bytes (v1.2.3.4)
            var message = new CANMessage(CANMessageProcessor.CAN_MSG_ID_VERSION_RESPONSE, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            // Act
            mockAdapter.Raise(a => a.MessageReceived += null, message);

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(1, receivedArgs.Major);
            Assert.Equal(2, receivedArgs.Minor);
            Assert.Equal("1.2.3", receivedArgs.VersionString);
        }
    }
}
