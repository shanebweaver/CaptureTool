using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.CaptureOverlay;
using Moq;
using System.Drawing;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureOverlayViewModelAudioInputTests
{
    [TestMethod]
    public async Task StartVideoCaptureCommand_ShouldWaitForRecordingStartedBeforeAdvancingTimer()
    {
        TestContext context = CreateViewModel([]);
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        await Task.Delay(250);

        Assert.IsTrue(context.ViewModel.IsRecording);
        Assert.AreEqual(TimeSpan.Zero, context.ViewModel.CaptureTime);

        context.VideoCaptureState.Raise(state => state.RecordingStarted += null!, EventArgs.Empty);
        for (int i = 0; i < 20 && context.ViewModel.CaptureTime == TimeSpan.Zero; i++)
        {
            await Task.Delay(50);
        }

        Assert.IsTrue(context.ViewModel.CaptureTime > TimeSpan.Zero);

        context.ViewModel.Dispose();
    }

    [TestMethod]
    public void AudioInputSourcesChanged_ShouldSelectDefaultInputAndAppendDefaultSuffix()
    {
        AudioInputSource[] sources =
        [
            new("external", "External microphone", false),
            new("default", "Built-in microphone", true)
        ];
        TestContext context = CreateViewModel(sources);

        context.ViewModel.Load(CreateOptions());
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, sources));

        Assert.IsTrue(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.AreEqual("default", context.ViewModel.SelectedAudioInputSource?.Id);
        Assert.AreEqual("Built-in microphone (Default)", context.ViewModel.SelectedAudioInputSource?.DisplayName);
        Assert.AreEqual(1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.SelectAudioInputSource.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SelectAudioInputSourceRequest>(request => request.SourceId == "default"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenNoInputsFound_ShouldDisableInputSelection()
    {
        TestContext context = CreateViewModel([]);

        context.ViewModel.Load(CreateOptions());
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, []));

        Assert.IsFalse(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.IsTrue(context.ViewModel.IsAudioInputMuted);
        Assert.IsNull(context.ViewModel.SelectedAudioInputSource);
        Assert.AreEqual(-1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.SelectAudioInputSource.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SelectAudioInputSourceRequest>(request => request.SourceId == null),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SetVideoCaptureAudioInputMutedRequest>(request => request.IsMuted),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenInputBecomesAvailable_ShouldStayMuted()
    {
        AudioInputSource[] sources =
        [
            new("default", "Built-in microphone", true)
        ];
        TestContext context = CreateViewModel([]);

        context.ViewModel.Load(CreateOptions());
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, []));
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.Added, sources));

        Assert.IsTrue(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.IsTrue(context.ViewModel.IsAudioInputMuted);
        Assert.AreEqual("default", context.ViewModel.SelectedAudioInputSource?.Id);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SetVideoCaptureAudioInputMutedRequest>(request => request.IsMuted),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenSelectedInputIsRemoved_ShouldMuteInputSelection()
    {
        AudioInputSource[] sources =
        [
            new("external", "External microphone", false),
            new("default", "Built-in microphone", true)
        ];
        AudioInputSource[] updatedSources =
        [
            new("external", "External microphone", true)
        ];
        TestContext context = CreateViewModel(sources);

        context.ViewModel.Load(CreateOptions());
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, sources));
        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.Removed, updatedSources));

        Assert.IsTrue(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.IsTrue(context.ViewModel.IsAudioInputMuted);
        Assert.AreEqual("external", context.ViewModel.SelectedAudioInputSource?.Id);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SetVideoCaptureAudioInputMutedRequest>(request => request.IsMuted),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static TestContext CreateViewModel(IReadOnlyList<AudioInputSource> sources)
    {
        Mock<IGetAudioInputSourcesUseCase> getAudioInputSources = new();
        getAudioInputSources
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetAudioInputSourcesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetAudioInputSourcesResponse>.Success(new GetAudioInputSourcesResponse(sources)));

        Mock<IAudioInputDetectionService> audioInputDetection = new();

        Mock<ITaskEnvironment> taskEnvironment = new();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        Mock<IThemeService> themeService = new();
        themeService.Setup(service => service.DefaultTheme).Returns(AppTheme.Light);
        themeService.Setup(service => service.CurrentTheme).Returns(AppTheme.Light);

        Mock<IStartVideoCaptureUseCase> startVideoCapture = new();
        startVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<StartVideoCaptureResponse>.Success(new StartVideoCaptureResponse()));

        Mock<IPrepareVideoCaptureUseCase> prepareVideoCapture = new();
        prepareVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<PrepareVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<PrepareVideoCaptureResponse>.Success(new PrepareVideoCaptureResponse()));

        Mock<ISelectAudioInputSourceUseCase> selectAudioInputSource = new();
        selectAudioInputSource
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<SelectAudioInputSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SelectAudioInputSourceRequest request, CancellationToken _) =>
                UseCaseResponse<SelectAudioInputSourceResponse>.Success(new SelectAudioInputSourceResponse(!string.IsNullOrWhiteSpace(request.SourceId), false)));

        Mock<ISetVideoCaptureAudioInputMutedUseCase> setAudioInputMuted = new();
        setAudioInputMuted
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<SetVideoCaptureAudioInputMutedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SetVideoCaptureAudioInputMutedResponse>.Success(new SetVideoCaptureAudioInputMutedResponse()));

        Mock<IVideoCaptureState> videoCaptureState = new();

        CaptureOverlayViewModel viewModel = new(
            Mock.Of<ICloseCaptureOverlayUseCase>(),
            Mock.Of<IGoBackFromCaptureOverlayUseCase>(),
            startVideoCapture.Object,
            Mock.Of<IStopVideoCaptureUseCase>(),
            Mock.Of<IToggleVideoCaptureDesktopAudioUseCase>(),
            Mock.Of<IToggleVideoCapturePauseResumeUseCase>(),
            prepareVideoCapture.Object,
            getAudioInputSources.Object,
            selectAudioInputSource.Object,
            setAudioInputMuted.Object,
            audioInputDetection.Object,
            themeService.Object,
            videoCaptureState.Object,
            taskEnvironment.Object);

        return new TestContext(viewModel, audioInputDetection, videoCaptureState, selectAudioInputSource, setAudioInputMuted);
    }

    private static CaptureOverlayViewModelOptions CreateOptions()
    {
        MonitorCaptureResult monitor = new(
            IntPtr.Zero,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new CaptureOverlayViewModelOptions(monitor, new Rectangle(0, 0, 1920, 1080));
    }

    private sealed record TestContext(
        CaptureOverlayViewModel ViewModel,
        Mock<IAudioInputDetectionService> AudioInputDetection,
        Mock<IVideoCaptureState> VideoCaptureState,
        Mock<ISelectAudioInputSourceUseCase> SelectAudioInputSource,
        Mock<ISetVideoCaptureAudioInputMutedUseCase> SetAudioInputMuted);
}
