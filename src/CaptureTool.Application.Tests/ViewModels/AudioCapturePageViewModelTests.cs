using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
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

        Fixture.Freeze<Mock<IAudioCapturePlayUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureStopUseCase>>();
        Fixture.Freeze<Mock<IAudioCapturePauseUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureMuteUseCase>>();
        Fixture.Freeze<Mock<IAudioCaptureToggleDesktopAudioUseCase>>();
        Fixture.Freeze<Mock<ITelemetryService>>();
    }

    [TestMethod]
    public void PlayCommand_ShouldInvokeAction_AndUpdateState_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var playAction = Fixture.Freeze<Mock<IAudioCapturePlayUseCase>>();
        var vm = Create();

        // Assert initial state
        Assert.IsTrue(vm.CanPlay);
        Assert.IsFalse(vm.IsPlaying);

        // Act
        vm.PlayCommand.Execute();

        // Assert
        playAction.Verify(a => a.Execute(), Times.Once);
        Assert.IsTrue(vm.IsPlaying);
        Assert.IsFalse(vm.IsPaused);
        Assert.IsFalse(vm.CanPlay);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Play, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Play, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void StopCommand_ShouldInvokeAction_AndUpdateState_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var stopAction = Fixture.Freeze<Mock<IAudioCaptureStopUseCase>>();
        var vm = Create();

        // Start playing first
        vm.PlayCommand.Execute();
        Assert.IsFalse(vm.CanPlay);

        // Act
        vm.StopCommand.Execute();

        // Assert
        stopAction.Verify(a => a.Execute(), Times.Once);
        Assert.IsFalse(vm.IsPlaying);
        Assert.IsFalse(vm.IsPaused);
        Assert.IsTrue(vm.CanPlay);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Stop, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Stop, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void PauseCommand_ShouldInvokeAction_AndToggleState_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var pauseAction = Fixture.Freeze<Mock<IAudioCapturePauseUseCase>>();
        var vm = Create();

        // Act - First pause
        vm.PauseCommand.Execute();

        // Assert - First pause
        pauseAction.Verify(a => a.Execute(), Times.Once);
        Assert.IsTrue(vm.IsPaused);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Once);

        // Act - Second pause (toggle off)
        vm.PauseCommand.Execute();

        // Assert - Second pause
        pauseAction.Verify(a => a.Execute(), Times.Exactly(2));
        Assert.IsFalse(vm.IsPaused);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Exactly(2));
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Pause, It.IsAny<string>()), Times.Exactly(2));
    }

    [TestMethod]
    public void MuteCommand_ShouldInvokeAction_AndToggleState_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var muteAction = Fixture.Freeze<Mock<IAudioCaptureMuteUseCase>>();
        var vm = Create();

        // Act - First mute
        vm.MuteCommand.Execute();

        // Assert - First mute
        muteAction.Verify(a => a.Execute(), Times.Once);
        Assert.IsTrue(vm.IsMuted);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Once);

        // Act - Second mute (toggle off)
        vm.MuteCommand.Execute();

        // Assert - Second mute
        muteAction.Verify(a => a.Execute(), Times.Exactly(2));
        Assert.IsFalse(vm.IsMuted);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Exactly(2));
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.Mute, It.IsAny<string>()), Times.Exactly(2));
    }

    [TestMethod]
    public void ToggleDesktopAudioCommand_ShouldInvokeAction_AndToggleState_AndTrackTelemetry()
    {
        // Arrange
        var telemetryService = Fixture.Freeze<Mock<ITelemetryService>>();
        var toggleDesktopAudioAction = Fixture.Freeze<Mock<IAudioCaptureToggleDesktopAudioUseCase>>();
        var vm = Create();

        // Act - First toggle
        vm.ToggleDesktopAudioCommand.Execute();

        // Assert - First toggle
        toggleDesktopAudioAction.Verify(a => a.Execute(), Times.Once);
        Assert.IsTrue(vm.IsDesktopAudioEnabled);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Once);
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Once);

        // Act - Second toggle (toggle off)
        vm.ToggleDesktopAudioCommand.Execute();

        // Assert - Second toggle
        toggleDesktopAudioAction.Verify(a => a.Execute(), Times.Exactly(2));
        Assert.IsFalse(vm.IsDesktopAudioEnabled);
        telemetryService.Verify(t => t.ActivityInitiated(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Exactly(2));
        telemetryService.Verify(t => t.ActivityCompleted(AudioCapturePageViewModel.ActivityIds.ToggleDesktopAudio, It.IsAny<string>()), Times.Exactly(2));
    }
}
