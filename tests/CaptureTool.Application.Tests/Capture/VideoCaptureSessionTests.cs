using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Capture.Video;
using FluentAssertions;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class VideoCaptureSessionTests
{
    [TestMethod]
    public void CreateRecordingOptions_ShouldUseSessionTargetPathAndAudioSettings()
    {
        var target = CaptureRecordingTarget.Rectangle(10, 1, 2, 3, 4);
        var audio = new VideoCaptureAudioSettings(false, false, 42, "microphone");
        var session = new VideoCaptureSession(@"C:\Temp\capture.mp4", target, audio);

        CaptureRecordingOptions options = session.CreateRecordingOptions();

        options.Target.Should().Be(target);
        options.OutputPath.Should().Be(@"C:\Temp\capture.mp4");
        options.CaptureAudio.Should().BeTrue();
        options.AudioInputSourceId.Should().Be("microphone");
        options.AudioInputVolumePercentage.Should().Be(42);
    }

    [TestMethod]
    public void SetPaused_ShouldTransitionBetweenRecordingAndPaused()
    {
        VideoCaptureSession session = CreateSession();

        session.SetPaused(true).Should().BeTrue();
        session.Status.Should().Be(VideoCaptureStatus.Paused);

        session.SetPaused(false).Should().BeTrue();
        session.Status.Should().Be(VideoCaptureStatus.Recording);
    }

    [TestMethod]
    public void BeginFinalizing_ShouldCreatePendingVideoAndRejectFurtherPauseChanges()
    {
        VideoCaptureSession session = CreateSession();

        var pendingVideo = session.BeginFinalizing();

        pendingVideo.FilePath.Should().Be(@"C:\Temp\capture.mp4");
        session.PendingVideo.Should().BeSameAs(pendingVideo);
        session.Status.Should().Be(VideoCaptureStatus.Finalizing);
        session.Invoking(s => s.SetPaused(true)).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void TryMarkRecordingStarted_ShouldOnlySucceedOnce()
    {
        VideoCaptureSession session = CreateSession();

        session.TryMarkRecordingStarted().Should().BeTrue();
        session.TryMarkRecordingStarted().Should().BeFalse();
    }

    private static VideoCaptureSession CreateSession()
        => new(
            @"C:\Temp\capture.mp4",
            CaptureRecordingTarget.Monitor(1),
            VideoCaptureAudioSettings.Default);
}
