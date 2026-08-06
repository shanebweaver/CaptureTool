using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Overlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureDesktopAudioVolume;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Capture.Video.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.Localization;
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

        Task startTask = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        await Task.Delay(250);

        Assert.IsTrue(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.AreEqual(TimeSpan.Zero, context.ViewModel.CaptureTime);

        context.VideoCaptureState.Raise(state => state.RecordingStarted += null!, EventArgs.Empty);
        await startTask;
        for (int i = 0; i < 20 && context.ViewModel.CaptureTime == TimeSpan.Zero; i++)
        {
            await Task.Delay(50);
        }

        Assert.IsTrue(context.ViewModel.CaptureTime > TimeSpan.Zero);
        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsTrue(context.ViewModel.IsRecording);

        context.ViewModel.Dispose();
    }

    [TestMethod]
    [DataRow(CaptureType.Rectangle, 0L)]
    [DataRow(CaptureType.Window, 0x1234L)]
    [DataRow(CaptureType.FullScreen, 0L)]
    public async Task StartVideoCaptureCommand_ShouldPreserveCaptureTarget(CaptureType captureType, long windowHandle)
    {
        TestContext context = CreateViewModel([]);
        StartVideoCaptureRequest? capturedRequest = null;
        context.StartVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .Callback<StartVideoCaptureRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(UseCaseResponse<StartVideoCaptureResponse>.Success(new StartVideoCaptureResponse()));
        Rectangle area = new(31, 47, 1280, 720);

        context.ViewModel.Load(CreateOptions(captureType, (nint)windowHandle, area));
        Task startTask = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        for (int i = 0; i < 20 && capturedRequest == null; i++)
        {
            await Task.Delay(10);
        }
        context.VideoCaptureState.Raise(state => state.RecordingStarted += null!, EventArgs.Empty);
        await startTask;

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(captureType, capturedRequest.CaptureArgs.CaptureType);
        Assert.AreEqual((nint)windowHandle, capturedRequest.CaptureArgs.WindowHandle);
        Assert.AreEqual(area, capturedRequest.CaptureArgs.Area);
        Assert.AreEqual((nint)123, capturedRequest.CaptureArgs.Monitor.HMonitor);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task StartVideoCaptureCommand_WhenRecordingStartedTimesOut_ShouldCancelAndResetState()
    {
        TestContext context = CreateViewModel([], TimeSpan.FromMilliseconds(50));
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.IsTrue(context.ViewModel.HasRecordingError);
        context.CancelVideoCapture.Verify(useCase => useCase.ExecuteAsync(
            It.Is<CancelVideoCaptureRequest>(request =>
                request.SkipConfirmation &&
                request.Reason == CancelVideoCaptureReason.StartTimeout),
            It.IsAny<CancellationToken>()), Times.Once);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task Dispose_WhileRecordingIsStarting_ShouldCancelWaitAndIgnoreLateStartedEvent()
    {
        TestContext context = CreateViewModel([], TimeSpan.FromSeconds(1));
        context.ViewModel.Load(CreateOptions());
        Task startTask = context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);
        Assert.IsTrue(context.ViewModel.IsStarting);

        context.ViewModel.Dispose();
        context.VideoCaptureState.Raise(state => state.RecordingStarted += null!, EventArgs.Empty);
        await startTask;

        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.HasRecordingError);
    }

    [TestMethod]
    public void Dispose_WhenCalledMoreThanOnce_ShouldReleaseResourcesOnce()
    {
        TestContext context = CreateViewModel([]);
        context.ViewModel.Load(CreateOptions());

        context.ViewModel.Dispose();
        context.ViewModel.Dispose();

        context.AudioInputDetection.Verify(
            service => service.StopWatching(),
            Times.Once);
    }

    [TestMethod]
    public async Task StartVideoCaptureCommand_WhenUseCaseFails_ShouldResetStateAndShowError()
    {
        TestContext context = CreateViewModel([]);
        context.StartVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<StartVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<StartVideoCaptureResponse>.Failure());
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.IsPaused);
        Assert.AreEqual(TimeSpan.Zero, context.ViewModel.CaptureTime);
        Assert.IsTrue(context.ViewModel.HasRecordingError);
        Assert.AreEqual("Recording couldn't start. Try again.", context.ViewModel.RecordingErrorMessage);
        context.ViewModel.Dispose();
    }

    [TestMethod]
    public async Task StartVideoCaptureCommand_WhenUseCaseReportsUnsupported_ShouldShowUnsupportedError()
    {
        TestContext context = CreateViewModel([]);
        context.StartVideoCapture
            .Setup(service => service.ExecuteAsync(
                It.IsAny<StartVideoCaptureRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<StartVideoCaptureResponse>.Success(
                new StartVideoCaptureResponse(
                    Succeeded: false,
                    FailureReason: StartVideoCaptureFailureReason.NotSupported)));
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.StartVideoCaptureCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsStarting);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.AreEqual("Screen recording isn't supported on this device.", context.ViewModel.RecordingErrorMessage);
        context.StartVideoCapture.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<StartVideoCaptureRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        Assert.IsFalse(context.ViewModel.IsAudioInputMuted);
        Assert.IsNull(context.ViewModel.SelectedAudioInputSource);
        Assert.AreEqual(-1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.SelectAudioInputSource.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SelectAudioInputSourceRequest>(request => request.SourceId == null),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<SetVideoCaptureAudioInputMutedRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenInputBecomesAvailable_ShouldSelectDefaultWithoutMuting()
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
        Assert.IsFalse(context.ViewModel.IsAudioInputMuted);
        Assert.AreEqual("default", context.ViewModel.SelectedAudioInputSource?.Id);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<SetVideoCaptureAudioInputMutedRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenSelectedInputIsRemoved_ShouldSelectReplacementWithoutMuting()
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
        Assert.IsFalse(context.ViewModel.IsAudioInputMuted);
        Assert.AreEqual("external", context.ViewModel.SelectedAudioInputSource?.Id);
        context.SetAudioInputMuted.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<SetVideoCaptureAudioInputMutedRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ToggleAudioInputMuteCommand_WhenUseCaseFails_DoesNotChangeDisplayedState()
    {
        TestContext context = CreateViewModel([new("default", "Built-in microphone", true)]);
        context.SetAudioInputMuted
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<SetVideoCaptureAudioInputMutedRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SetVideoCaptureAudioInputMutedResponse>.Failure());
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.ToggleAudioInputMuteCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsAudioInputMuted);
    }

    [TestMethod]
    public async Task ToggleDesktopAudioCommand_WhenRecordingStartedMuted_UsesCommittedEnabledState()
    {
        TestContext context = CreateViewModel([]);
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.ToggleDesktopAudioCommand.ExecuteAsync(null);

        Assert.IsTrue(context.ViewModel.IsDesktopAudioEnabled);
        context.ToggleDesktopAudio.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<ToggleVideoCaptureDesktopAudioRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ToggleDesktopAudioCommand_WhenUseCaseFails_PreservesMutedState()
    {
        TestContext context = CreateViewModel([]);
        context.ToggleDesktopAudio
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<ToggleVideoCaptureDesktopAudioRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ToggleVideoCaptureDesktopAudioResponse>.Failure());
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.ToggleDesktopAudioCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsDesktopAudioEnabled);
    }

    [TestMethod]
    public async Task SetDesktopAudioVolumeCommand_WhenUseCaseSucceeds_UsesCommittedValue()
    {
        TestContext context = CreateViewModel([]);
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.SetDesktopAudioVolumeCommand.ExecuteAsync(64);

        Assert.AreEqual(64, context.ViewModel.DesktopAudioVolumePercentage);
        context.SetDesktopAudioVolume.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SetVideoCaptureDesktopAudioVolumeRequest>(request => request.VolumePercentage == 64),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SetDesktopAudioVolumeCommand_WhenUseCaseFails_RestoresCommittedValue()
    {
        TestContext context = CreateViewModel([]);
        context.SetDesktopAudioVolume
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<SetVideoCaptureDesktopAudioVolumeRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SetVideoCaptureDesktopAudioVolumeResponse>.Failure());
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.SetDesktopAudioVolumeCommand.ExecuteAsync(64);

        Assert.AreEqual(100, context.ViewModel.DesktopAudioVolumePercentage);
    }

    [TestMethod]
    public async Task ToggleAudioInputMuteCommand_WhenUseCaseSucceeds_UsesCommittedStateEvent()
    {
        TestContext context = CreateViewModel([new("default", "Built-in microphone", true)]);
        context.ViewModel.Load(CreateOptions());

        await context.ViewModel.ToggleAudioInputMuteCommand.ExecuteAsync(null);

        Assert.IsTrue(context.ViewModel.IsAudioInputMuted);
    }

    [TestMethod]
    public async Task SelectAudioInputSourceCommand_WhenUseCaseFails_PreservesCommittedSelection()
    {
        AudioInputSource[] sources =
        [
            new("external", "External microphone", false),
            new("default", "Built-in microphone", true)
        ];
        TestContext context = CreateViewModel(sources);
        context.ViewModel.Load(CreateOptions());
        context.SelectAudioInputSource
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<SelectAudioInputSourceRequest>(request => request.SourceId == "external"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SelectAudioInputSourceResponse>.Failure());

        await context.ViewModel.SelectAudioInputSourceCommand.ExecuteAsync(sources[0]);

        Assert.AreEqual("default", context.ViewModel.SelectedAudioInputSource?.Id);
    }

    private static TestContext CreateViewModel(
        IReadOnlyList<AudioInputSource> sources,
        TimeSpan? recordingStartTimeout = null)
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

        Mock<ICancelVideoCaptureUseCase> cancelVideoCapture = new();
        cancelVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<CancelVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<CancelVideoCaptureResponse>.Success(new CancelVideoCaptureResponse()));

        Mock<IPrepareVideoCaptureUseCase> prepareVideoCapture = new();
        prepareVideoCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<PrepareVideoCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<PrepareVideoCaptureResponse>.Success(new PrepareVideoCaptureResponse()));

        string? selectedAudioInputSourceId = null;
        bool isAudioInputMuted = false;
        bool isDesktopAudioEnabled = false;
        int desktopAudioVolumePercentage = 100;
        Mock<IVideoCaptureState> videoCaptureState = new();
        videoCaptureState.SetupGet(state => state.SelectedAudioInputSourceId).Returns(() => selectedAudioInputSourceId);
        videoCaptureState.SetupGet(state => state.IsAudioInputMuted).Returns(() => isAudioInputMuted);
        videoCaptureState.SetupGet(state => state.IsDesktopAudioEnabled).Returns(() => isDesktopAudioEnabled);
        videoCaptureState.SetupGet(state => state.DesktopAudioVolumePercentage).Returns(() => desktopAudioVolumePercentage);

        Mock<IToggleVideoCaptureDesktopAudioUseCase> toggleDesktopAudio = new();
        toggleDesktopAudio
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<ToggleVideoCaptureDesktopAudioRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                isDesktopAudioEnabled = !isDesktopAudioEnabled;
                videoCaptureState.Raise(
                    state => state.DesktopAudioStateChanged += null!,
                    videoCaptureState.Object,
                    isDesktopAudioEnabled);
                return UseCaseResponse<ToggleVideoCaptureDesktopAudioResponse>.Success(
                    new ToggleVideoCaptureDesktopAudioResponse());
            });

        Mock<ISetVideoCaptureDesktopAudioVolumeUseCase> setDesktopAudioVolume = new();
        setDesktopAudioVolume
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<SetVideoCaptureDesktopAudioVolumeRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SetVideoCaptureDesktopAudioVolumeRequest request, CancellationToken _) =>
            {
                desktopAudioVolumePercentage = Math.Clamp(request.VolumePercentage, 0, 100);
                videoCaptureState.Raise(
                    state => state.DesktopAudioVolumeChanged += null!,
                    videoCaptureState.Object,
                    desktopAudioVolumePercentage);
                return UseCaseResponse<SetVideoCaptureDesktopAudioVolumeResponse>.Success(
                    new SetVideoCaptureDesktopAudioVolumeResponse());
            });

        Mock<ISelectAudioInputSourceUseCase> selectAudioInputSource = new();
        selectAudioInputSource
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<SelectAudioInputSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SelectAudioInputSourceRequest request, CancellationToken _) =>
            {
                selectedAudioInputSourceId = request.SourceId;
                videoCaptureState.Raise(
                    state => state.AudioInputSourceChanged += null!,
                    videoCaptureState.Object,
                    request.SourceId!);
                return UseCaseResponse<SelectAudioInputSourceResponse>.Success(
                    new SelectAudioInputSourceResponse(!string.IsNullOrWhiteSpace(request.SourceId), false));
            });

        Mock<ISetVideoCaptureAudioInputMutedUseCase> setAudioInputMuted = new();
        setAudioInputMuted
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<SetVideoCaptureAudioInputMutedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SetVideoCaptureAudioInputMutedRequest request, CancellationToken _) =>
            {
                isAudioInputMuted = request.IsMuted;
                videoCaptureState.Raise(
                    state => state.AudioInputMutedStateChanged += null!,
                    videoCaptureState.Object,
                    request.IsMuted);
                return UseCaseResponse<SetVideoCaptureAudioInputMutedResponse>.Success(
                    new SetVideoCaptureAudioInputMutedResponse());
            });

        Mock<ILocalizationService> localizationService = new();
        localizationService
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns((string resourceKey) => resourceKey switch
            {
                "CaptureOverlay_RecordingUnsupportedMessage" => "Screen recording isn't supported on this device.",
                _ => "Recording couldn't start. Try again."
            });

        CaptureOverlayViewModel viewModel = new(
            Mock.Of<ICloseCaptureOverlayUseCase>(),
            Mock.Of<IGoBackFromCaptureOverlayUseCase>(),
            startVideoCapture.Object,
            cancelVideoCapture.Object,
            Mock.Of<IStopVideoCaptureUseCase>(),
            toggleDesktopAudio.Object,
            setDesktopAudioVolume.Object,
            Mock.Of<IToggleVideoCapturePauseResumeUseCase>(),
            prepareVideoCapture.Object,
            getAudioInputSources.Object,
            selectAudioInputSource.Object,
            setAudioInputMuted.Object,
            audioInputDetection.Object,
            themeService.Object,
            videoCaptureState.Object,
            taskEnvironment.Object,
            localizationService.Object,
            recordingStartTimeout);

        return new TestContext(
            viewModel,
            audioInputDetection,
            videoCaptureState,
            startVideoCapture,
            cancelVideoCapture,
            toggleDesktopAudio,
            setDesktopAudioVolume,
            selectAudioInputSource,
            setAudioInputMuted);
    }

    private static CaptureOverlayViewModelOptions CreateOptions(
        CaptureType captureType = CaptureType.Rectangle,
        nint windowHandle = 0,
        Rectangle? area = null)
    {
        MonitorCaptureResult monitor = new(
            123,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new CaptureOverlayViewModelOptions(
            new NewCaptureArgs(
                monitor,
                area ?? new Rectangle(0, 0, 1920, 1080),
                captureType,
                windowHandle));
    }

    private sealed record TestContext(
        CaptureOverlayViewModel ViewModel,
        Mock<IAudioInputDetectionService> AudioInputDetection,
        Mock<IVideoCaptureState> VideoCaptureState,
        Mock<IStartVideoCaptureUseCase> StartVideoCapture,
        Mock<ICancelVideoCaptureUseCase> CancelVideoCapture,
        Mock<IToggleVideoCaptureDesktopAudioUseCase> ToggleDesktopAudio,
        Mock<ISetVideoCaptureDesktopAudioVolumeUseCase> SetDesktopAudioVolume,
        Mock<ISelectAudioInputSourceUseCase> SelectAudioInputSource,
        Mock<ISetVideoCaptureAudioInputMutedUseCase> SetAudioInputMuted);
}
