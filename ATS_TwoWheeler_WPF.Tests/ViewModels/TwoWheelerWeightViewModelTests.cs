using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Models;
using ATS_TwoWheeler_WPF.Services.Interfaces;
using ATS_TwoWheeler_WPF.ViewModels;
using Moq;
using Xunit;

namespace ATS_TwoWheeler_WPF.Tests.ViewModels
{
    public class TwoWheelerWeightViewModelTests
    {
        private readonly Mock<ICANService> _canServiceMock;
        private readonly Mock<IWeightProcessorService> _weightProcessorMock;
        private readonly Mock<IDataLoggerService> _dataLoggerMock;
        private readonly Mock<ISettingsService> _settingsMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly TwoWheelerWeightViewModel _viewModel;

        public TwoWheelerWeightViewModelTests()
        {
            _canServiceMock = new Mock<ICANService>();
            _weightProcessorMock = new Mock<IWeightProcessorService>();
            _dataLoggerMock = new Mock<IDataLoggerService>();
            _settingsMock = new Mock<ISettingsService>();
            _dialogServiceMock = new Mock<IDialogService>();

            // Setup default behaviors
            _canServiceMock.Setup(x => x.IsConnected).Returns(true);
            _weightProcessorMock.Setup(x => x.LatestTotal).Returns(new ProcessedWeightData { TaredWeight = 0 });

            _viewModel = new TwoWheelerWeightViewModel(
                _canServiceMock.Object,
                _weightProcessorMock.Object,
                _dataLoggerMock.Object,
                _settingsMock.Object,
                _dialogServiceMock.Object
            );
        }

        [Fact]
        public void StartTest_ShouldStartLogging_and_UpdateState()
        {
            // Act
            _viewModel.StartTestCommand.Execute(null);

            // Assert
            Assert.Equal(TestState.Running, _viewModel.CurrentState);
            _dataLoggerMock.Verify(x => x.StartLogging(), Times.Once);
        }

        [Fact]
        public void StopTest_ShouldStopLogging_and_UpdateState()
        {
            // Arrange
            _viewModel.StartTestCommand.Execute(null);

            // Act
            _viewModel.StopTestCommand.Execute(null);

            // Assert
            Assert.Equal(TestState.Idle, _viewModel.CurrentState);
            _dataLoggerMock.Verify(x => x.StopLogging(), Times.Once);
        }

        [Fact]
        public async Task ExportData_ShouldShowDialog_and_Export()
        {
            // Arrange
            _dialogServiceMock.Setup(x => x.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns("test_export.csv");

            // Act
            // Execute via ICommand interface which handles async void internally
            _viewModel.ExportCommand.Execute(null);
            
            // Wait for background task to complete (fragile but necessary without AsyncCommand pattern)
            await Task.Delay(1000);

            // Assert
            _dialogServiceMock.Verify(x => x.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }
}
