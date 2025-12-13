using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Helpers;
using Moq;

namespace CaptureTool.ViewModels.Tests;

[TestClass]
public sealed class HomePageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private HomePageViewModel Create() => Fixture.Create<HomePageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        Fixture.Freeze<Mock<IHomeActions>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void NewImageCaptureCommand_ShouldInvokeHomeActions_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var homeActions = Fixture.Freeze<Mock<IHomeActions>>();
        var vm = Create();

        vm.NewImageCaptureCommand.Execute(null);

        homeActions.Verify(h => h.NewImageCapture(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewImageCapture), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewImageCapture), Times.Once);
    }

    [TestMethod]
    public void NewVideoCaptureCommand_ShouldInvokeHomeActions_AndTrackTelemetry_WhenEnabled()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture)).Returns(true);

        var homeActions = Fixture.Freeze<Mock<IHomeActions>>();
        var vm = Create();

        vm.NewVideoCaptureCommand.Execute(null);

        homeActions.Verify(h => h.NewVideoCapture(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewVideoCapture), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewVideoCapture), Times.Once);
    }
}
