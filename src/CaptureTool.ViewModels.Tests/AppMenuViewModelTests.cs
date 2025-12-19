using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.AppMenu;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels;
using Moq;

namespace CaptureTool.ViewModels.Tests;

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

        Fixture.Freeze<Mock<IAppMenuActions>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<IImageCaptureHandler>>();
        Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        Fixture.Freeze<Mock<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>>();
    }

    [TestMethod]
    public void ExitApplicationCommand_ShouldCallExitApplication_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var appMenuActions = Fixture.Freeze<Mock<IAppMenuActions>>();
        var vm = Create();

        // Act
        vm.ExitApplicationCommand.Execute(null);

        // Assert
        appMenuActions.Verify(a => a.ExitApplication(), Times.Once, "ExitApplication() should be called when user clicks Exit in menu");
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.ExitApplication), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.ExitApplication), Times.Once);
    }

    [TestMethod]
    public void NavigateToSettingsCommand_ShouldNavigate_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var appMenuActions = Fixture.Freeze<Mock<IAppMenuActions>>();
        var vm = Create();

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert
        appMenuActions.Verify(a => a.NavigateToSettings(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.NavigateToSettings), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.NavigateToSettings), Times.Once);
    }
}
