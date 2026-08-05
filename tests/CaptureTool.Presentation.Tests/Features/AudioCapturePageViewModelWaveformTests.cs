using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.AudioCapture;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class AudioCapturePageViewModelWaveformTests
{
    [TestMethod]
    public void AddWaveformLevel_WhenViewportIsFull_ShouldKeepNewestLevelsVisible()
    {
        AudioCapturePageViewModel viewModel = CreateViewModel();

        for (int levelIndex = 0; levelIndex < 140; levelIndex++)
        {
            viewModel.AddWaveformLevel(levelIndex / 200d);
        }

        viewModel.WaveformBars.Should().HaveCount(128);
        viewModel.WaveformBars.Select(bar => bar.Level).Should().Equal(
            Enumerable.Range(12, 128).Select(levelIndex => levelIndex / 200d));
        viewModel.WaveformBars[0].Height.Should().BeApproximately(7.92, .001);
        viewModel.WaveformBars[^1].Height.Should().BeApproximately(91.74, .001);
        viewModel.Dispose();
    }

    [TestMethod]
    public void CaptureStateChanged_WhenStopFails_ResetsPageAndShowsRecoverableError()
    {
        var audioCaptureState = new TestAudioCaptureState();
        var localizationService = new Mock<ILocalizationService>();
        localizationService
            .Setup(service => service.GetString("AudioCapture_StopCaptureFailedMessage"))
            .Returns("The recording could not be finalized.");
        AudioCapturePageViewModel viewModel = CreateViewModel(audioCaptureState, localizationService.Object);
        audioCaptureState.Raise(new AudioCaptureStateChange(AudioCaptureState.Recording));
        viewModel.AddWaveformLevel(.75);

        audioCaptureState.Raise(new AudioCaptureStateChange(
            AudioCaptureState.Stopped,
            new AudioCaptureFailure(AudioCaptureFailureStage.RecorderStop, "Platform error")));

        viewModel.IsRecording.Should().BeFalse();
        viewModel.IsPaused.Should().BeFalse();
        viewModel.CanStartRecording.Should().BeTrue();
        viewModel.CaptureTime.Should().Be(TimeSpan.Zero);
        viewModel.WaveformBars.Should().BeEmpty();
        viewModel.HasCaptureError.Should().BeTrue();
        viewModel.CaptureErrorMessage.Should().Be("The recording could not be finalized.");

        viewModel.DismissCaptureErrorCommand.Execute(null);

        viewModel.HasCaptureError.Should().BeFalse();
        viewModel.Dispose();
    }

    [TestMethod]
    public void CaptureStateChanged_WhenRecordingRestarts_ClearsPreviousFailure()
    {
        var audioCaptureState = new TestAudioCaptureState();
        AudioCapturePageViewModel viewModel = CreateViewModel(audioCaptureState);
        audioCaptureState.Raise(new AudioCaptureStateChange(
            AudioCaptureState.Stopped,
            new AudioCaptureFailure(AudioCaptureFailureStage.PostProcessing, "Completion error")));

        audioCaptureState.Raise(new AudioCaptureStateChange(AudioCaptureState.Recording));

        viewModel.IsRecording.Should().BeTrue();
        viewModel.CanStartRecording.Should().BeFalse();
        viewModel.HasCaptureError.Should().BeFalse();
        viewModel.Dispose();
    }

    private static AudioCapturePageViewModel CreateViewModel(
        IAudioCaptureState? audioCaptureState = null,
        ILocalizationService? localizationService = null)
    {
        Mock<IAudioInputDetectionService> audioInputDetectionService = new();
        audioInputDetectionService
            .Setup(service => service.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<ITaskEnvironment> taskEnvironment = new();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        return new AudioCapturePageViewModel(
            audioCaptureState ?? new TestAudioCaptureState(),
            audioInputDetectionService.Object,
            Mock.Of<IStartAudioCaptureUseCase>(),
            Mock.Of<IStopAudioCaptureUseCase>(),
            Mock.Of<ICancelAudioCaptureUseCase>(),
            Mock.Of<IPauseAudioCaptureUseCase>(),
            Mock.Of<IMuteAudioCaptureUseCase>(),
            Mock.Of<ISelectAudioCaptureInputSourceUseCase>(),
            Mock.Of<IToggleLocalAudioCaptureUseCase>(),
            taskEnvironment.Object,
            Mock.Of<IAudioWaveformHistory>(),
            localizationService ?? Mock.Of<ILocalizationService>());
    }

    private sealed class TestAudioCaptureState : IAudioCaptureState
    {
        public event EventHandler<AudioCaptureStateChange>? CaptureStateChanged;
        public event EventHandler<bool>? MutedStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? DesktopAudioStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string?>? AudioInputSourceChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<AudioFile>? NewAudioCaptured
        {
            add { }
            remove { }
        }

        public event EventHandler<AudioCaptureLevel>? AudioLevelCaptured
        {
            add { }
            remove { }
        }

        public bool IsRecording { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsMuted { get; set; }
        public bool IsDesktopAudioEnabled { get; set; } = true;
        public string? SelectedAudioInputSourceId { get; set; }
        public AudioCaptureState CaptureState { get; private set; }

        public void Raise(AudioCaptureStateChange change)
        {
            CaptureState = change.State;
            IsRecording = change.State is AudioCaptureState.Recording or AudioCaptureState.Paused;
            IsPaused = change.State == AudioCaptureState.Paused;
            CaptureStateChanged?.Invoke(this, change);
        }
    }
}
