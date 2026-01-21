using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Capture;
using CaptureTool.Domains.Capture.Interfaces;
using FluentAssertions;
using Moq;

namespace CaptureTool.Core.Tests.Capture;

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
        
        // Manually set IsRecording to true using reflection to avoid StartVideoCapture complexity
        var isRecordingField = typeof(CaptureToolVideoCaptureHandler).GetProperty("IsRecording");
        isRecordingField?.SetValue(handler, true);

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
        
        // Manually set IsRecording to true using reflection to avoid StartVideoCapture complexity
        var isRecordingField = typeof(CaptureToolVideoCaptureHandler).GetProperty("IsRecording");
        isRecordingField?.SetValue(handler, true);

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
        var storageService = Fixture.Freeze<Mock<CaptureTool.Infrastructure.Interfaces.Storage.IStorageService>>();
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
        handler.IsPaused.Should().BeFalse();
    }

    [TestMethod]
    public void CancelVideoCapture_ShouldResetIsPausedToFalse()
    {
        // Arrange
        var screenRecorder = Fixture.Freeze<Mock<IScreenRecorder>>();
        var storageService = Fixture.Freeze<Mock<CaptureTool.Infrastructure.Interfaces.Storage.IStorageService>>();
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
        handler.IsPaused.Should().BeFalse();
    }
}
