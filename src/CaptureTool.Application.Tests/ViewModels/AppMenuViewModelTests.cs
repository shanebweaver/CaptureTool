using CaptureTool.Application.Implementations.ViewModels;
using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Interfaces.UseCases.AppMenu;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public sealed class AppMenuViewModelTests
{
    public required IFixture Fixture { get; set; }

    private AppMenuViewModel Create() => Fixture.Create<AppMenuViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));

        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        Fixture.Freeze<Mock<IAppMenuUseCases>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<IImageCaptureHandler>>();
        Fixture.Freeze<Mock<IVideoCaptureHandler>>();
    }

    [TestMethod]
    public void ExitApplicationCommand_ShouldCallShutdown_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var appMenuActions = Fixture.Freeze<Mock<IAppMenuUseCases>>();
        var vm = Create();

        // Act
        vm.ExitApplicationCommand.Execute();

        // Assert
        appMenuActions.Verify(a => a.ExitApplication(), Times.Once, "ExitApplication() should be called when user clicks Exit in menu");
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.ExitApplication, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.ExitApplication, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void NavigateToSettingsCommand_ShouldNavigate_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var appMenuActions = Fixture.Freeze<Mock<IAppMenuUseCases>>();
        var vm = Create();

        // Act
        vm.NavigateToSettingsCommand.Execute();

        // Assert
        appMenuActions.Verify(a => a.NavigateToSettings(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.NavigateToSettings, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.NavigateToSettings, It.IsAny<string>()), Times.Once);
    }
}
