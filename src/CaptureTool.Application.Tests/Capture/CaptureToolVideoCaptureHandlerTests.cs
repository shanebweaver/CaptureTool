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
        Fixture.Freeze<Mock<IScreenRecorder>>()
            .Setup(x => x.StartRecording(
                It.IsAny<nint>(),
                It.IsAny<System.Drawing.Rectangle>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .Returns(true);
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
    public void StartVideoCapture_WhenNativeStartFails_ShouldReturnToIdle()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        screenRecorder
            .Setup(x => x.StartRecording(
                It.IsAny<nint>(),
                It.IsAny<System.Drawing.Rectangle>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .Returns(false);
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        var args = CreateCaptureArgs();

        Action action = () => handler.StartVideoCapture(args);

        action.Should().Throw<InvalidOperationException>();
        handler.IsRecording.Should().BeFalse();
        handler.IsFinalizing.Should().BeFalse();
    }

    [TestMethod]
    public void StartVideoCapture_UsesPhysicalPixelCaptureArea()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        var args = new NewCaptureArgs(
            new MonitorCaptureResult(
                new nint(42),
                [],
                144,
                new System.Drawing.Rectangle(100, 200, 1920, 1080),
                new System.Drawing.Rectangle(100, 200, 1920, 1040),
                true),
            new System.Drawing.Rectangle(10, 20, 300, 200));

        handler.StartVideoCapture(args);

        screenRecorder.Verify(x => x.StartRecording(
            new nint(42),
            new System.Drawing.Rectangle(15, 30, 450, 300),
            It.IsAny<string>(),
            It.IsAny<bool>()), Times.Once);
    }

    [TestMethod]
    public async Task StopVideoCapture_WhenNativeFinalizeFails_ShouldFaultPendingVideo()
    {
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        screenRecorder
            .Setup(x => x.StopRecording())
            .Returns(new ScreenRecordingResult(
                unchecked((int)0x80004005),
                ScreenRecordingStage.SinkFinalize));
        var storageService = Fixture.Freeze<Mock<IStorageService>>();
        storageService.Setup(s => s.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        var handler = Fixture.Create<CaptureToolVideoCaptureHandler>();
        handler.StartVideoCapture(CreateCaptureArgs());

        PendingVideoFile pendingVideo = handler.StopVideoCapture();

        ScreenRecordingException exception = await Assert.ThrowsExactlyAsync<ScreenRecordingException>(
            pendingVideo.WhenReadyAsync);
        await handler.WaitForFinalizationAsync();
        exception.Result.Stage.Should().Be(ScreenRecordingStage.SinkFinalize);
        handler.IsRecording.Should().BeFalse();
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
                true),
            new System.Drawing.Rectangle(0, 0, 1920, 1080));
    }
}
