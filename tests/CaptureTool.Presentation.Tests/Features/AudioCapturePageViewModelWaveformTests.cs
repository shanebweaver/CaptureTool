using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.TaskEnvironment;
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
    }

    private static AudioCapturePageViewModel CreateViewModel()
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
            Mock.Of<IAudioCaptureState>(),
            audioInputDetectionService.Object,
            Mock.Of<IStartAudioCaptureUseCase>(),
            Mock.Of<IStopAudioCaptureUseCase>(),
            Mock.Of<IPauseAudioCaptureUseCase>(),
            Mock.Of<IMuteAudioCaptureUseCase>(),
            Mock.Of<ISelectAudioCaptureInputSourceUseCase>(),
            Mock.Of<IToggleLocalAudioCaptureUseCase>(),
            taskEnvironment.Object,
            Mock.Of<IAudioWaveformHistory>());
    }
}
