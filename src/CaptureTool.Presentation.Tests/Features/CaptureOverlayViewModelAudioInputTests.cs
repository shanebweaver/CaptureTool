using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
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
        context.VideoCaptureHandler.Verify(handler => handler.SelectAudioInputSource("default"), Times.AtLeastOnce);
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
        Assert.IsNull(context.ViewModel.SelectedAudioInputSource);
        Assert.AreEqual(-1, context.ViewModel.SelectedAudioInputSourceIndex);
        context.VideoCaptureHandler.Verify(handler => handler.SelectAudioInputSource(null), Times.AtLeastOnce);
    }

    private static TestContext CreateViewModel(IReadOnlyList<AudioInputSource> sources)
    {
        Mock<IGetAudioInputSourcesUseCase> getAudioInputSources = new();
        getAudioInputSources
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetAudioInputSourcesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetAudioInputSourcesResponse>.Success(new GetAudioInputSourcesResponse(sources)));

        Mock<IAudioInputDetectionService> audioInputDetection = new();

        Mock<IAudioInputSelectionFeatureAvailability> featureAvailability = new();
        featureAvailability.Setup(service => service.IsAudioInputSelectionEnabled).Returns(true);

        Mock<ITaskEnvironment> taskEnvironment = new();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        Mock<IThemeService> themeService = new();
        themeService.Setup(service => service.DefaultTheme).Returns(AppTheme.Light);
        themeService.Setup(service => service.CurrentTheme).Returns(AppTheme.Light);

        Mock<IVideoCaptureHandler> videoCaptureHandler = new();

        CaptureOverlayViewModel viewModel = new(
            Mock.Of<ICloseCaptureOverlayUseCase>(),
            Mock.Of<IGoBackFromCaptureOverlayUseCase>(),
            Mock.Of<IStartVideoCaptureUseCase>(),
            Mock.Of<IStopVideoCaptureUseCase>(),
            Mock.Of<IToggleVideoCaptureDesktopAudioUseCase>(),
            Mock.Of<IToggleVideoCapturePauseResumeUseCase>(),
            getAudioInputSources.Object,
            Mock.Of<ISelectAudioInputSourceUseCase>(),
            audioInputDetection.Object,
            featureAvailability.Object,
            themeService.Object,
            videoCaptureHandler.Object,
            taskEnvironment.Object);

        return new TestContext(viewModel, audioInputDetection, videoCaptureHandler);
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
        Mock<IVideoCaptureHandler> VideoCaptureHandler);
}
