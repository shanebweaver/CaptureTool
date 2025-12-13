using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Services.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public class ErrorPageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private ErrorPageViewModel Create() => Fixture.Create<ErrorPageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IErrorActions>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void RestartAppCommand_ShouldInvokeErrorActions_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var errorActions = Fixture.Freeze<Mock<IErrorActions>>();
        var vm = Create();

        // Act
        vm.RestartAppCommand.Execute(null);

        // Assert
        errorActions.Verify(a => a.RestartApp(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(ErrorPageViewModel.ActivityIds.RestartApp), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(ErrorPageViewModel.ActivityIds.RestartApp), Times.Once);
    }
}
