using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.AudioCapture;
using CommunityToolkit.Mvvm.Input;
using Moq;

namespace CaptureTool.Application.Tests.ViewModels;

[TestClass]
public sealed class AudioCapturePageViewModelTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeStateFromAudioCaptureState()
    {
        TestContext context = CreateViewModel([]);

        Assert.IsTrue(context.ViewModel.CanStartRecording);
        Assert.IsFalse(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.IsPaused);
        Assert.IsFalse(context.ViewModel.IsMuted);
        Assert.IsFalse(context.ViewModel.IsDesktopAudioEnabled);
    }

    [TestMethod]
    public async Task StartCommand_ShouldInvokeStartUseCase()
    {
        TestContext context = CreateViewModel([]);

        context.ViewModel.StartCommand.Execute(null);
        await ((IAsyncRelayCommand)context.ViewModel.StartCommand).ExecutionTask!;

        context.StartAudioCapture.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<StartAudioCaptureRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void CaptureStateChanged_ShouldRefreshRecordingState()
    {
        TestContext context = CreateViewModel([]);

        context.AudioCaptureState.SetupGet(state => state.IsRecording).Returns(true);
        context.AudioCaptureState.SetupGet(state => state.IsPaused).Returns(false);
        context.AudioCaptureState.Raise(state => state.CaptureStateChanged += null, context.AudioCaptureState.Object, AudioCaptureState.Recording);

        Assert.IsTrue(context.ViewModel.IsRecording);
        Assert.IsFalse(context.ViewModel.CanStartRecording);
    }

    [TestMethod]
    public void CaptureStateChanged_ShouldRefreshPausedState()
    {
        TestContext context = CreateViewModel([]);

        context.AudioCaptureState.SetupGet(state => state.IsRecording).Returns(true);
        context.AudioCaptureState.SetupGet(state => state.IsPaused).Returns(true);
        context.AudioCaptureState.Raise(state => state.CaptureStateChanged += null, context.AudioCaptureState.Object, AudioCaptureState.Paused);

        Assert.IsTrue(context.ViewModel.IsPaused);
    }

    [TestMethod]
    public void MutedStateChanged_ShouldRefreshMutedState()
    {
        TestContext context = CreateViewModel([]);

        context.AudioCaptureState.Raise(state => state.MutedStateChanged += null, context.AudioCaptureState.Object, true);

        Assert.IsTrue(context.ViewModel.IsMuted);
    }

    [TestMethod]
    public void DesktopAudioStateChanged_ShouldRefreshDesktopAudioState()
    {
        TestContext context = CreateViewModel([]);

        context.AudioCaptureState.Raise(state => state.DesktopAudioStateChanged += null, context.AudioCaptureState.Object, true);

        Assert.IsTrue(context.ViewModel.IsDesktopAudioEnabled);
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

        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, sources));

        Assert.IsTrue(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.AreEqual("default", context.ViewModel.SelectedAudioInputSource?.Id);
        Assert.AreEqual("Built-in microphone (Default)", context.ViewModel.SelectedAudioInputSource?.DisplayName);
        Assert.AreEqual(1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.SelectAudioInputSource.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SelectAudioCaptureInputSourceRequest>(request => request.SourceId == "default"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public void AudioInputSourcesChanged_WhenNoInputsFound_ShouldClearSelectionAndMute()
    {
        TestContext context = CreateViewModel([]);

        context.AudioInputDetection.Raise(
            service => service.AudioInputSourcesChanged += null!,
            new AudioInputSourcesChangedEventArgs(AudioInputSourcesChangeReason.EnumerationCompleted, []));

        Assert.IsFalse(context.ViewModel.IsAudioInputSelectionAvailable);
        Assert.IsNull(context.ViewModel.SelectedAudioInputSource);
        Assert.AreEqual(-1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.SelectAudioInputSource.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SelectAudioCaptureInputSourceRequest>(request => request.SourceId == null),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        context.MuteAudioCapture.Verify(useCase => useCase.ExecuteAsync(
            It.IsAny<MuteAudioCaptureRequest>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static TestContext CreateViewModel(IReadOnlyList<AudioInputSource> sources)
    {
        Mock<IAudioCaptureState> audioCaptureState = new();
        audioCaptureState.SetupGet(state => state.IsRecording).Returns(false);
        audioCaptureState.SetupGet(state => state.IsPaused).Returns(false);
        audioCaptureState.SetupGet(state => state.IsMuted).Returns(false);
        audioCaptureState.SetupGet(state => state.IsDesktopAudioEnabled).Returns(false);

        Mock<IAudioInputDetectionService> audioInputDetection = new();
        audioInputDetection
            .Setup(service => service.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);

        Mock<ITaskEnvironment> taskEnvironment = new();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        Mock<IStartAudioCaptureUseCase> startAudioCapture = new();
        startAudioCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<StartAudioCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<StartAudioCaptureResponse>.Success(new StartAudioCaptureResponse()));

        Mock<IMuteAudioCaptureUseCase> muteAudioCapture = new();
        muteAudioCapture
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<MuteAudioCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<MuteAudioCaptureResponse>.Success(new MuteAudioCaptureResponse()));

        Mock<ISelectAudioCaptureInputSourceUseCase> selectAudioInputSource = new();
        selectAudioInputSource
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<SelectAudioCaptureInputSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<SelectAudioCaptureInputSourceResponse>.Success(new SelectAudioCaptureInputSourceResponse()));

        AudioCapturePageViewModel viewModel = new(
            audioCaptureState.Object,
            audioInputDetection.Object,
            startAudioCapture.Object,
            Mock.Of<IStopAudioCaptureUseCase>(),
            Mock.Of<IPauseAudioCaptureUseCase>(),
            muteAudioCapture.Object,
            selectAudioInputSource.Object,
            Mock.Of<IToggleLocalAudioCaptureUseCase>(),
            taskEnvironment.Object);

        return new TestContext(
            viewModel,
            audioCaptureState,
            audioInputDetection,
            startAudioCapture,
            muteAudioCapture,
            selectAudioInputSource);
    }

    private sealed record TestContext(
        AudioCapturePageViewModel ViewModel,
        Mock<IAudioCaptureState> AudioCaptureState,
        Mock<IAudioInputDetectionService> AudioInputDetection,
        Mock<IStartAudioCaptureUseCase> StartAudioCapture,
        Mock<IMuteAudioCaptureUseCase> MuteAudioCapture,
        Mock<ISelectAudioCaptureInputSourceUseCase> SelectAudioInputSource);
}
