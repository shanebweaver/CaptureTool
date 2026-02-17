using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public class AudioCapturePageViewModelTests
{
    public required IFixture Fixture { get; set; }

    private AudioCapturePageViewModel Create() => Fixture.Create<AudioCapturePageViewModel>();

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });

        var audioCaptureHandler = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        audioCaptureHandler.Setup(s => s.IsRecording).Returns(false);
        audioCaptureHandler.Setup(s => s.IsPaused).Returns(false);
        audioCaptureHandler.Setup(s => s.IsMuted).Returns(false);
        audioCaptureHandler.Setup(s => s.IsDesktopAudioEnabled).Returns(false);

        Fixture.Freeze<Mock<IAudioCaptureStartUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureStopUseCase>>();
        Fixture.Freeze<Mock<IAudioCapturePauseUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureMuteUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureToggleDesktopAudioUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void PlayCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var playAction = Fixture.Freeze<Mock<IAudioCaptureStartUseCase>>();
        var vm = Create();

        // Assert initial state
        Assert.IsTrue(vm.CanPlay);
        Assert.IsFalse(vm.IsPlaying);

        // Act
        vm.PlayCommand.Execute();

        // Assert
        playAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Play, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Play, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void StopCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var stopAction = Fixture.Freeze<Mock<IAudioCaptureStopUseCase>>();
        var vm = Create();

        // Act
        vm.StopCommand.Execute();

        // Assert
        stopAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Stop, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Stop, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void PauseCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var pauseAction = Fixture.Freeze<Mock<IAudioCapturePauseUseCase>>();
        var vm = Create();

        // Act
        vm.PauseCommand.Execute();

        // Assert
        pauseAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void MuteCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var muteAction = Fixture.Freeze<Mock<IAudioCaptureMuteUseCase>>();
        var vm = Create();

        // Act
        vm.MuteCommand.Execute();

        // Assert
        muteAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void ToggleDesktopAudioCommand_ShouldInvokeAction_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var toggleDesktopAudioAction = Fixture.Freeze<Mock<IAudioCaptureToggleDesktopAudioUseCase>>();
        var vm = Create();

        // Act
        vm.ToggleDesktopAudioCommand.Execute();

        // Assert
        toggleDesktopAudioAction.Verify(a => a.Execute(), Times.Once);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void ViewModel_ShouldSyncStateFromService_WhenPlayingStateChanges()
    {
        // Arrange
        var audioCaptureHandler = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var vm = Create();

        Assert.IsFalse(vm.IsPlaying);
        Assert.IsTrue(vm.CanPlay);

        // Act
        audioCaptureHandler.Raise(s => s.CaptureStateChanged += null, audioCaptureHandler.Object, true);

        // Assert
        Assert.IsTrue(vm.IsPlaying);
        Assert.IsFalse(vm.CanPlay);
    }

    [TestMethod]
    public void ViewModel_ShouldSyncStateFromService_WhenPausedStateChanges()
    {
        // Arrange
        var audioCaptureHandler = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var vm = Create();

        Assert.IsFalse(vm.IsPaused);

        // Act
        audioCaptureHandler.Raise(s => s.CaptureStateChanged += null, audioCaptureHandler.Object, true);

        // Assert
        Assert.IsTrue(vm.IsPaused);
    }

    [TestMethod]
    public void ViewModel_ShouldSyncStateFromService_WhenMutedStateChanges()
    {
        // Arrange
        var audioCaptureHandler = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var vm = Create();

        Assert.IsFalse(vm.IsMuted);

        // Act
        audioCaptureHandler.Raise(s => s.MutedStateChanged += null, audioCaptureHandler.Object, true);

        // Assert
        Assert.IsTrue(vm.IsMuted);
    }

    [TestMethod]
    public void ViewModel_ShouldSyncStateFromService_WhenDesktopAudioStateChanges()
    {
        // Arrange
        var audioCaptureHandler = Fixture.Freeze<Mock<IAudioCaptureHandler>>();
        var vm = Create();

        Assert.IsFalse(vm.IsDesktopAudioEnabled);

        // Act
        audioCaptureHandler.Raise(s => s.DesktopAudioStateChanged += null, audioCaptureHandler.Object, true);

        // Assert
        Assert.IsTrue(vm.IsDesktopAudioEnabled);
    }
}
