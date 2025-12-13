using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Windowing;
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

        Fixture.Freeze<Mock<IShutdownHandler>>();
        Fixture.Freeze<Mock<IAppNavigation>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
        Fixture.Freeze<Mock<IWindowHandleProvider>>();
        Fixture.Freeze<Mock<IFilePickerService>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<IStorageService>>();
        Fixture.Freeze<Mock<IImageCaptureHandler>>();
        Fixture.Freeze<Mock<IVideoCaptureHandler>>();
    }

    [TestMethod]
    public void ExitApplicationCommand_ShouldCallShutdown_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var shutdownHandler = Fixture.Freeze<Mock<IShutdownHandler>>();
        var vm = Create();

        // Act
        vm.ExitApplicationCommand.Execute(null);

        // Assert
        shutdownHandler.Verify(s => s.Shutdown(), Times.Once, "Shutdown() should be called when user clicks Exit in menu");
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.ExitApplication), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.ExitApplication), Times.Once);
    }

    [TestMethod]
    public void NavigateToSettingsCommand_ShouldNavigate_AndTrackTelemetry()
    {
        // Arrange
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var appNavigation = Fixture.Freeze<Mock<IAppNavigation>>();
        var vm = Create();

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert
        appNavigation.Verify(n => n.GoToSettings(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(AppMenuViewModel.ActivityIds.NavigateToSettings), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(AppMenuViewModel.ActivityIds.NavigateToSettings), Times.Once);
    }
}
