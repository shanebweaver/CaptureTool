using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.ViewModels;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

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

        Fixture.Freeze<Mock<IHomeNewImageCaptureUseCase>>();
        Fixture.Freeze<Mock<IHomeNewVideoCaptureUseCase>>();
        Fixture.Freeze<Mock<IHomeNewAudioCaptureUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureFeatureAvailability>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void NewImageCaptureCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var newImageCaptureAction = Fixture.Freeze<Mock<IHomeNewImageCaptureUseCase>>();
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

        var newVideoCaptureAction = Fixture.Freeze<Mock<IHomeNewVideoCaptureUseCase>>();
        newVideoCaptureAction.Setup(a => a.CanExecute()).Returns(true);
        var vm = Create();

        vm.NewVideoCaptureCommand.Execute();

        newVideoCaptureAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewVideoCapture, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewVideoCapture, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void NewAudioCaptureCommand_ShouldInvokeAction_AndTrackTelemetry_WhenEnabled()
    {
        var telemetry = Fixture.Freeze<Mock<ITelemetryService>>();
        var featureAvailability = Fixture.Freeze<Mock<IAudioCaptureFeatureAvailability>>();
        featureAvailability.Setup(f => f.IsAudioCaptureEnabled).Returns(true);

        var newAudioCaptureAction = Fixture.Freeze<Mock<IHomeNewAudioCaptureUseCase>>();
        newAudioCaptureAction.Setup(a => a.CanExecute()).Returns(true);
        var vm = Create();

        vm.NewAudioCaptureCommand.Execute();

        newAudioCaptureAction.Verify(a => a.Execute(), Times.Once);
        telemetry.Verify(t => t.ActivityInitiated(HomePageViewModel.ActivityIds.NewAudioCapture, It.IsAny<string>()), Times.Once);
        telemetry.Verify(t => t.ActivityCompleted(HomePageViewModel.ActivityIds.NewAudioCapture, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void IsAudioCaptureEnabled_ShouldReflectFeatureFlag()
    {
        var featureAvailability = Fixture.Freeze<Mock<IAudioCaptureFeatureAvailability>>();
        featureAvailability.Setup(f => f.IsAudioCaptureEnabled).Returns(false);
        var vm = Create();
        Assert.IsFalse(vm.IsAudioCaptureEnabled);

        featureAvailability.Setup(f => f.IsAudioCaptureEnabled).Returns(true);
        vm = Create();
        Assert.IsTrue(vm.IsAudioCaptureEnabled);
    }
}
