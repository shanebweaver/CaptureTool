using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.AudioEdit;
using CaptureTool.Presentation.Features.Media;
using CaptureTool.Presentation.Features.VideoEdit;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class MediaFailureStateTests
{
    [TestMethod]
    public async Task PendingVideoFinalization_WhenItFails_EntersLoggedNonRetryableFailure()
    {
        var logService = new Mock<ILogService>();
        VideoEditPageViewModel viewModel = CreateVideoViewModel(logService.Object);
        var pendingVideo = new PendingVideoFile(@"C:\private\capture.mp4");
        var failure = new IOException("Encoder finalization failed.");
        viewModel.Load(pendingVideo);

        pendingVideo.Fail(failure);
        await WaitUntilAsync(() => viewModel.HasMediaFailure);

        viewModel.IsFinalizingVideo.Should().BeFalse();
        viewModel.IsVideoReady.Should().BeFalse();
        viewModel.MediaLoadState.Should().Be(MediaLoadState.Failed);
        viewModel.MediaFailureCategory.Should().Be(MediaFailureCategory.Finalization);
        viewModel.CanRetryMedia.Should().BeFalse();
        viewModel.MediaFailureMessage.Should().Be("The recording couldn't be finalized.");
        viewModel.MediaFailureMessage.Should().NotContain("capture.mp4");
        logService.Verify(
            service => service.LogException(failure, "Video finalization failed."),
            Times.Once);
    }

    [TestMethod]
    public void VideoPlayback_WhenUnsupported_ExposesBoundedRetryableFailure()
    {
        VideoEditPageViewModel viewModel = CreateVideoViewModel(Mock.Of<ILogService>());
        viewModel.Load(new VideoFile(@"C:\private\unsupported.mp4"));

        viewModel.ReportMediaFailed(MediaFailureCategory.Unsupported);

        viewModel.MediaLoadState.Should().Be(MediaLoadState.Failed);
        viewModel.MediaFailureCategory.Should().Be(MediaFailureCategory.Unsupported);
        viewModel.CanRetryMedia.Should().BeTrue();
        viewModel.MediaFailureMessage.Should().Be("This format or codec isn't supported.");
        viewModel.MediaFailureMessage.Should().NotContain("unsupported.mp4");

        viewModel.RetryMediaCommand.Execute(null);

        viewModel.MediaLoadState.Should().Be(MediaLoadState.Loading);
        viewModel.MediaFailureCategory.Should().BeNull();
        viewModel.MediaFailureMessage.Should().BeEmpty();
    }

    [TestMethod]
    public void VideoPlayback_WhenMediaOpens_TransitionsFromLoadingToReady()
    {
        VideoEditPageViewModel viewModel = CreateVideoViewModel(Mock.Of<ILogService>());
        viewModel.Load(new VideoFile("video.mp4"));

        viewModel.IsMediaLoading.Should().BeTrue();
        viewModel.IsMediaReady.Should().BeFalse();

        viewModel.ReportMediaOpened();

        viewModel.MediaLoadState.Should().Be(MediaLoadState.Ready);
        viewModel.IsMediaReady.Should().BeTrue();
        viewModel.HasMediaFailure.Should().BeFalse();
    }

    [TestMethod]
    public void AudioPlayback_WhenUnsupported_ExposesBoundedRetryableFailure()
    {
        AudioEditPageViewModel viewModel = CreateAudioViewModel();
        viewModel.Load(new AudioFile(@"C:\private\unsupported.wav"));

        viewModel.ReportMediaFailed(MediaFailureCategory.Unsupported);

        viewModel.MediaLoadState.Should().Be(MediaLoadState.Failed);
        viewModel.MediaFailureCategory.Should().Be(MediaFailureCategory.Unsupported);
        viewModel.CanRetryMedia.Should().BeTrue();
        viewModel.MediaFailureMessage.Should().Be("This format or codec isn't supported.");
        viewModel.MediaFailureMessage.Should().NotContain("unsupported.wav");

        viewModel.RetryMediaCommand.Execute(null);
        viewModel.MediaLoadState.Should().Be(MediaLoadState.Loading);

        viewModel.ReportMediaOpened();
        viewModel.IsMediaReady.Should().BeTrue();
    }

    private static VideoEditPageViewModel CreateVideoViewModel(ILogService logService)
    {
        return new VideoEditPageViewModel(
            Mock.Of<ISaveVideoFileUseCase>(),
            Mock.Of<ICopyVideoFileUseCase>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IOpenVideosFolderUseCase>(),
            logService,
            localizationService: CreateLocalizationService());
    }

    private static AudioEditPageViewModel CreateAudioViewModel()
    {
        return new AudioEditPageViewModel(
            Mock.Of<ISaveAudioFileUseCase>(),
            Mock.Of<ICopyAudioFileUseCase>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IOpenAudioFolderUseCase>(),
            Mock.Of<IAudioWaveformHistory>(),
            localizationService: CreateLocalizationService());
    }

    private static ILocalizationService CreateLocalizationService()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns<string>(resourceKey => resourceKey switch
            {
                "MediaFailure_Finalization" => "The recording couldn't be finalized.",
                "MediaFailure_FileUnavailable" => "This media file is unavailable.",
                "MediaFailure_Unsupported" => "This format or codec isn't supported.",
                "MediaFailure_Playback" => "This media couldn't be played.",
                _ => resourceKey
            });
        return localizationService.Object;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
