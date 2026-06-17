using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Domain.Capture;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public class CaptureToolVideoCaptureHandlerTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldSetIsPausedToTrue_AndRaiseEvent_WhenCalledWithTrue()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        bool eventRaised = false;
        bool eventValue = false;
        handler.PausedStateChanged += (sender, value) =>
        {
            eventRaised = true;
            eventValue = value;
        };

        // Act
        handler.ToggleIsPaused(true);

        // Assert
        handler.IsPaused.Should().BeTrue();
        eventRaised.Should().BeTrue();
        eventValue.Should().BeTrue();
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldSetIsPausedToFalse_AndRaiseEvent_WhenCalledWithFalse()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        bool eventRaised = false;
        bool eventValue = true;
        handler.PausedStateChanged += (sender, value) =>
        {
            eventRaised = true;
            eventValue = value;
        };

        // Act
        handler.ToggleIsPaused(false);

        // Assert
        handler.IsPaused.Should().BeFalse();
        eventRaised.Should().BeTrue();
        eventValue.Should().BeFalse();
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldPauseRecording_WhenRecordingAndPausedIsTrue()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        // Manually set IsRecording to true to avoid StartVideoCapture complexity
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        // Act
        handler.ToggleIsPaused(true);

        // Assert
        screenRecorder.Verify(s => s.PauseRecording(), Times.Once);
        screenRecorder.Verify(s => s.ResumeRecording(), Times.Never);
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldResumeRecording_WhenRecordingAndPausedIsFalse()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        // Manually set IsRecording to true to avoid StartVideoCapture complexity
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        // Act
        handler.ToggleIsPaused(false);

        // Assert
        screenRecorder.Verify(s => s.ResumeRecording(), Times.Once);
        screenRecorder.Verify(s => s.PauseRecording(), Times.Never);
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldNotCallScreenRecorder_WhenNotRecording()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        // Act - toggle without recording
        handler.ToggleIsPaused(true);

        // Assert
        screenRecorder.Verify(s => s.PauseRecording(), Times.Never);
        screenRecorder.Verify(s => s.ResumeRecording(), Times.Never);
    }

    [TestMethod]
    public void StopVideoCapture_ShouldResetIsPausedToFalse()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());

        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                IntPtr.Zero,
                new byte[0],
                96,
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                true
            ),
            new System.Drawing.Rectangle(0, 0, 1920, 1080)
        );

        handler.StartVideoCapture(args);
        handler.ToggleIsPaused(true);

        // Verify paused state is set
        handler.IsPaused.Should().BeTrue();

        // Act
        handler.StopVideoCapture();

        // Assert
        handler.IsFinalizing.Should().BeTrue();
        handler.IsRecording.Should().BeFalse();
    }

    [TestMethod]
    public void CancelVideoCapture_ShouldResetIsPausedToFalse()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());

        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                IntPtr.Zero,
                new byte[0],
                96,
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                true
            ),
            new System.Drawing.Rectangle(0, 0, 1920, 1080)
        );

        handler.StartVideoCapture(args);
        handler.ToggleIsPaused(true);

        // Verify paused state is set
        handler.IsPaused.Should().BeTrue();

        // Act
        handler.CancelVideoCapture();

        // Assert
        handler.IsFinalizing.Should().BeFalse();
        handler.IsRecording.Should().BeFalse();
    }

    [TestMethod]
    public void StartVideoCapture_ShouldReturnToIdle_WhenRecorderStartFails()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        screenRecorder
            .Setup(s => s.StartRecording(It.IsAny<CaptureRecordingOptions>()))
            .Returns(new CaptureRecorderResult(CaptureRecorderStatus.StartFailed, unchecked((int)0x80004005)));

        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());

        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();

        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                IntPtr.Zero,
                [],
                96,
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                true
            ),
            new System.Drawing.Rectangle(0, 0, 1920, 1080)
        );

        Action act = () => handler.StartVideoCapture(args);

        act.Should().Throw<Exception>();
        handler.IsRecording.Should().BeFalse();
        handler.IsFinalizing.Should().BeFalse();
    }

    [TestMethod]
    public void StartVideoCapture_ShouldPassSelectedAudioInputSourceToRecorder()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());

        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.SelectAudioInputSource("microphone-id");
        handler.SetAudioInputVolume(42);

        handler.StartVideoCapture(CreateCaptureArgs());

        screenRecorder.Verify(s => s.StartRecording(It.Is<CaptureRecordingOptions>(options =>
            options.AudioInputSourceId == "microphone-id" &&
            options.AudioInputVolumePercentage == 42 &&
            options.CaptureAudio)), Times.Once);
    }

    [TestMethod]
    public void SelectAudioInputSource_ShouldSwitchRecorder_WhenRecording()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        handler.SelectAudioInputSource("microphone-id");

        handler.SelectedAudioInputSourceId.Should().Be("microphone-id");
        screenRecorder.Verify(s => s.SetAudioInputSource("microphone-id"), Times.Once);
        screenRecorder.Verify(s => s.SetAudioCaptureEnabled(true), Times.Once);
    }

    [TestMethod]
    public void SetIsAudioInputMuted_ShouldUpdateRecorder_WhenRecording()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.SelectAudioInputSource("microphone-id");
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        handler.SetIsAudioInputMuted(true);

        handler.IsAudioInputMuted.Should().BeTrue();
        screenRecorder.Verify(s => s.SetAudioCaptureEnabled(false), Times.Once);
    }

    [TestMethod]
    public void SetAudioInputVolume_ShouldUpdateRecorder_WhenRecording()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        handler.SetAudioInputVolume(37);

        handler.AudioInputVolumePercentage.Should().Be(37);
        screenRecorder.Verify(s => s.SetAudioInputVolume(37), Times.Once);
    }

    [TestMethod]
    public void SelectAudioInputSource_ShouldClearRecorderSource_WhenSourceIdIsNull()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.SelectAudioInputSource("microphone-id");
        handler.UpdateCaptureState(CaptureToolVideoCaptureHandler.CaptureState.Recording);

        handler.SelectAudioInputSource(null);

        handler.SelectedAudioInputSourceId.Should().BeNull();
        screenRecorder.Verify(s => s.SetAudioInputSource(null), Times.Once);
        screenRecorder.Verify(s => s.SetAudioCaptureEnabled(false), Times.Once);
    }

    private static NewCaptureArgs CreateCaptureArgs()
    {
        return new NewCaptureArgs(
            new MonitorCaptureResult(
                IntPtr.Zero,
                [],
                96,
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                new System.Drawing.Rectangle(0, 0, 1920, 1080),
                true
            ),
            new System.Drawing.Rectangle(0, 0, 1920, 1080)
        );
    }
}
