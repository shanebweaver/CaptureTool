using CaptureTool.Application.Implementations.ViewModels;
using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.UseCases.Error;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

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

        Fixture.Freeze<Mock<IErrorRestartAppUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void RestartAppCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var restartAppAction = Fixture.Freeze<Mock<IErrorRestartAppUseCase>>();
        var vm = Create();

        // Act
        vm.RestartAppCommand.Execute(null);

        // Assert
        restartAppAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(ErrorPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(ErrorPageViewModel.ActivityIds.RestartApp, It.IsAny<string>()), Times.Once);
    }
}
