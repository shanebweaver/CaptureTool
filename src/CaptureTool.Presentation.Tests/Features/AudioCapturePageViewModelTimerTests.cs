using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Features.AudioCapture;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class AudioCapturePageViewModelTimerTests
{
    [TestMethod]
    public async Task RecordingState_ShouldWaitForRecordingStartedBeforeAdvancingTimer()
    {
        bool isRecording = false;
        bool isPaused = false;
        Mock<IAudioCaptureHandler> audioCaptureHandler = new();
        audioCaptureHandler.SetupGet(handler => handler.IsRecording).Returns(() => isRecording);
        audioCaptureHandler.SetupGet(handler => handler.IsPaused).Returns(() => isPaused);
        audioCaptureHandler.SetupGet(handler => handler.IsMuted).Returns(false);
        audioCaptureHandler.SetupGet(handler => handler.IsDesktopAudioEnabled).Returns(true);

        AudioCapturePageViewModel viewModel = CreateViewModel(audioCaptureHandler);

        isRecording = true;
        audioCaptureHandler.Raise(
            handler => handler.CaptureStateChanged += null!,
            audioCaptureHandler.Object,
            AudioCaptureState.Recording);
        await Task.Delay(250);

        Assert.IsTrue(viewModel.IsRecording);
        Assert.AreEqual(TimeSpan.Zero, viewModel.CaptureTime);

        audioCaptureHandler.Raise(
            handler => handler.RecordingStarted += null!,
            audioCaptureHandler.Object,
            EventArgs.Empty);
        for (int i = 0; i < 20 && viewModel.CaptureTime == TimeSpan.Zero; i++)
        {
            await Task.Delay(50);
        }

        Assert.IsTrue(viewModel.CaptureTime > TimeSpan.Zero);

        viewModel.Dispose();
    }

    private static AudioCapturePageViewModel CreateViewModel(Mock<IAudioCaptureHandler> audioCaptureHandler)
    {
        Mock<IAudioInputDetectionService> audioInputDetection = new();
        audioInputDetection
            .Setup(service => service.GetAudioInputSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<ITaskEnvironment> taskEnvironment = new();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        return new AudioCapturePageViewModel(
            audioCaptureHandler.Object,
            audioInputDetection.Object,
            Mock.Of<IStartAudioCaptureUseCase>(),
            Mock.Of<IStopAudioCaptureUseCase>(),
            Mock.Of<IPauseAudioCaptureUseCase>(),
            Mock.Of<IMuteAudioCaptureUseCase>(),
            Mock.Of<IToggleLocalAudioCaptureUseCase>(),
            taskEnvironment.Object);
    }
}
