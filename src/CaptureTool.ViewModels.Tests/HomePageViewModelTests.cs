using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Interfaces.Actions.Home;
using CaptureTool.Core.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Telemetry;
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

        Fixture.Freeze<Mock<IHomeNewImageCaptureAction>>();
        Fixture.Freeze<Mock<IHomeNewVideoCaptureAction>>();
        Fixture.Freeze<Mock<IFeatureManager>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void NewImageCaptureCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var newImageCaptureAction = Fixture.Freeze<Mock<IHomeNewImageCaptureAction>>();
        newImageCaptureAction.Setup(a => a.CanExecute()).Returns(true);
        var vm = Create();

        vm.NewImageCaptureCommand.Execute();

        newImageCaptureAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewImageCapture, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewImageCapture, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void NewVideoCaptureCommand_ShouldInvokeAction_AndTrackTelemetry_WhenEnabled()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var featureManager = Fixture.Freeze<Mock<IFeatureManager>>();
        featureManager.Setup(f => f.IsEnabled(CaptureToolFeatures.Feature_VideoCapture)).Returns(true);

        var newVideoCaptureAction = Fixture.Freeze<Mock<IHomeNewVideoCaptureAction>>();
        var vm = Create();

        vm.NewVideoCaptureCommand.Execute();

        newVideoCaptureAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewVideoCapture, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewVideoCapture, It.IsAny<string>()), Times.Once);
    }
}
