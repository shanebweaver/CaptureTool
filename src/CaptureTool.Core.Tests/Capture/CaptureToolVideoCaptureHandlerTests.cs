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
}
