using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class AudioCaptureNavigationGuardTests
{
    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenNotRecording_DoesNotPrompt()
    {
        var audioCaptureHandler = new Mock<IAudioCaptureHandler>();
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        audioCaptureHandler.SetupGet(handler => handler.IsRecording).Returns(false);
        var guard = new AudioCaptureNavigationGuard(audioCaptureHandler.Object, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsTrue(canNavigate);
        confirmationService.Verify(
            service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        audioCaptureHandler.Verify(handler => handler.StopCapture(), Times.Never);
    }

    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenRecordingAndUserCancels_DoesNotStopCapture()
    {
        var audioCaptureHandler = new Mock<IAudioCaptureHandler>();
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        audioCaptureHandler.SetupGet(handler => handler.IsRecording).Returns(true);
        confirmationService
            .Setup(service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var guard = new AudioCaptureNavigationGuard(audioCaptureHandler.Object, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsFalse(canNavigate);
        audioCaptureHandler.Verify(handler => handler.StopCapture(), Times.Never);
    }

    [TestMethod]
    public async Task CanNavigateAwayFromActiveCaptureAsync_WhenRecordingAndUserConfirms_StopsCapture()
    {
        var audioCaptureHandler = new Mock<IAudioCaptureHandler>();
        var confirmationService = new Mock<IAudioCaptureNavigationConfirmationService>();
        audioCaptureHandler.SetupGet(handler => handler.IsRecording).Returns(true);
        audioCaptureHandler
            .Setup(handler => handler.StopCapture())
            .Returns(new AudioFile("recording.wav"));
        confirmationService
            .Setup(service => service.ConfirmStopActiveRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var guard = new AudioCaptureNavigationGuard(audioCaptureHandler.Object, confirmationService.Object);

        bool canNavigate = await guard.CanNavigateAwayFromActiveCaptureAsync(TestContext.CancellationToken);

        Assert.IsTrue(canNavigate);
        audioCaptureHandler.Verify(handler => handler.StopCapture(), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
