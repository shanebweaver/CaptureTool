using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Capture.Audio;
using Moq;

namespace CaptureTool.Application.Tests.Capture.Audio;

[TestClass]
public sealed class AudioCaptureNavigationGuardTests
{
    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenNotRecording_DoesNotPrompt()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = false };
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        var guard = new AudioCaptureNavigationGuard(audioCapture, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsTrue(canNavigate);
        confirmationService.Verify(
            service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.AreEqual(0, audioCapture.StopCallCount);
    }

    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenRecordingAndUserCancels_DoesNotStopCapture()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var guard = new AudioCaptureNavigationGuard(audioCapture, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsFalse(canNavigate);
        Assert.AreEqual(0, audioCapture.StopCallCount);
    }

    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenRecordingAndUserConfirms_StopsCapture()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var guard = new AudioCaptureNavigationGuard(audioCapture, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsTrue(canNavigate);
        Assert.AreEqual(1, audioCapture.StopCallCount);
    }

    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_AfterStopFailure_DoesNotPromptAgain()
    {
        var audioCapture = new FakeAudioCaptureWorkflow
        {
            IsRecording = true,
            StopException = new InvalidOperationException("Recorder stop failed."),
        };
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var guard = new AudioCaptureNavigationGuard(audioCapture, confirmationService.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken));
        bool canNavigateAfterFailure = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsTrue(canNavigateAfterFailure);
        Assert.AreEqual(1, audioCapture.StopCallCount);
        confirmationService.Verify(
            service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
