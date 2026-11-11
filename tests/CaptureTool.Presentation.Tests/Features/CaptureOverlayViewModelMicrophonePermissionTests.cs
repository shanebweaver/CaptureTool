using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.UseCases;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

public sealed partial class CaptureOverlayViewModelAudioInputTests
{
    [TestMethod]
    public async Task RecordingWaitsForMicrophoneConsentBeforeStarting()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        var answer = new TaskCompletionSource<MicrophoneAccessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        permission.Setup(service => service.RequestAccessAsync(It.IsAny<CancellationToken>())).Returns(answer.Task);
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object);
        context.ViewModel.Load(CreateOptions());
        StartImmediately(context);
        Task start = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsTrue(context.ViewModel.IsStarting);
        context.StartVideoCapture.Verify(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        answer.SetResult(MicrophoneAccessResult.Granted);
        await start;
        Assert.IsTrue(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    [DataRow(MicrophoneAccessResult.SettingsRequired, 1)]
    [DataRow(MicrophoneAccessResult.Declined, 0)]
    public async Task DeniedPermissionDoesNotStartOrShowRecordingErrorAndCanBeRetried(MicrophoneAccessResult result, int prompts)
    {
        var permission = new Mock<IMicrophoneAccessService>();
        permission.SetupSequence(service => service.RequestAccessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result).ReturnsAsync(MicrophoneAccessResult.Granted);
        var prompt = new Mock<IMicrophoneAccessPrompt>();
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        StartImmediately(context);
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
        prompt.Verify(service => service.ShowAsync(It.IsAny<CancellationToken>()), Times.Exactly(prompts));
        context.StartVideoCapture.Verify(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsTrue(context.ViewModel.IsRecording);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MutedOrMissingMicrophoneDoesNotRequestPermission(bool hasMicrophone)
    {
        var permission = new Mock<IMicrophoneAccessService>(MockBehavior.Strict);
        var prompt = new Mock<IMicrophoneAccessPrompt>(MockBehavior.Strict);
        var context = CreateViewModel(hasMicrophone ? [new("mic", "Microphone", true)] : [], microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        if (hasMicrophone) await context.ViewModel.ToggleAudioInputMuteCommand.ExecuteAsync(null);
        StartImmediately(context);
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsTrue(context.ViewModel.IsRecording);
        permission.VerifyNoOtherCalls();
        prompt.VerifyNoOtherCalls();
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task UnmutingWithDeniedPermissionKeepsCurrentRecordingMuted()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        permission.Setup(service => service.RequestAccessAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MicrophoneAccessResult.SettingsRequired);
        var prompt = new Mock<IMicrophoneAccessPrompt>();
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        await context.ViewModel.ToggleAudioInputMuteCommand.ExecuteAsync(null);
        StartImmediately(context);
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        await context.ViewModel.ToggleAudioInputMuteCommand.ExecuteAsync(null);
        Assert.IsTrue(context.ViewModel.IsRecording);
        Assert.IsTrue(context.ViewModel.IsAudioInputMuted);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
        prompt.Verify(service => service.ShowAsync(It.IsAny<CancellationToken>()), Times.Once);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task ClosingOverlayDuringPermissionPromptCannotStartRecordingLater()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        var answer = new TaskCompletionSource<MicrophoneAccessResult>();
        CancellationToken token = default;
        permission.Setup(service => service.RequestAccessAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => { token = ct; return answer.Task; });
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object);
        context.ViewModel.Load(CreateOptions());
        Task start = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        context.ViewModel.Dispose();
        Assert.IsTrue(token.IsCancellationRequested);
        answer.SetResult(MicrophoneAccessResult.Granted);
        await start;
        context.StartVideoCapture.Verify(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
    }

    [TestMethod]
    public async Task SwitchingMicrophonesWhileRecordingRequiresPermissionAndPreservesSelectionOnDenial()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        permission.SetupSequence(service => service.RequestAccessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MicrophoneAccessResult.Granted).ReturnsAsync(MicrophoneAccessResult.SettingsRequired);
        var prompt = new Mock<IMicrophoneAccessPrompt>();
        var context = CreateViewModel([new("mic", "Microphone", true), new("other", "Other microphone", false)],
            microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        StartImmediately(context);
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        await context.ViewModel.SelectAudioInputSourceCommand.ExecuteAsync(context.ViewModel.AudioInputSources[1]);
        Assert.AreEqual("mic", context.ViewModel.SelectedAudioInputSource?.Id);
        Assert.IsTrue(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
        prompt.Verify(service => service.ShowAsync(It.IsAny<CancellationToken>()), Times.Once);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task ClosingOverlayDismissesTheSettingsPromptWithoutStartingCapture()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        permission.Setup(service => service.RequestAccessAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MicrophoneAccessResult.SettingsRequired);
        var prompt = new Mock<IMicrophoneAccessPrompt>();
        CancellationToken token = default;
        prompt.Setup(service => service.ShowAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => { token = ct; return Task.Delay(Timeout.Infinite, ct); });
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        Task start = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsTrue(token.CanBeCanceled);
        context.ViewModel.Dispose();
        await start;
        Assert.IsTrue(token.IsCancellationRequested);
        context.StartVideoCapture.Verify(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
    }

    [TestMethod]
    public async Task RecorderDenialAfterPreflightShowsSettingsPrompt()
    {
        var permission = new Mock<IMicrophoneAccessService>();
        permission.Setup(service => service.RequestAccessAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MicrophoneAccessResult.Granted);
        var prompt = new Mock<IMicrophoneAccessPrompt>();
        var context = CreateViewModel([new("mic", "Microphone", true)], microphoneAccess: permission.Object, microphonePrompt: prompt.Object);
        context.ViewModel.Load(CreateOptions());
        context.StartVideoCapture.Setup(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<StartVideoCaptureResponse>.Success(new(false, StartVideoCaptureFailureReason.MicrophoneAccessDenied)));
        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        prompt.Verify(service => service.ShowAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
        Assert.IsFalse(context.ViewModel.IsRecording);
        context.ViewModel.Dispose();
    }

    private static void StartImmediately(TestContext context) =>
        context.StartVideoCapture.Setup(service => service.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                context.VideoCaptureState.Raise(state => state.RecordingStarted += null!, EventArgs.Empty);
                return Task.FromResult(UseCaseResponse<StartVideoCaptureResponse>.Success(new()));
            });
}
