using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
using FluentAssertions;
using Moq;
using AppVideoCaptureNotSupportedException = CaptureTool.Application.Abstractions.Capture.VideoCaptureNotSupportedException;
using AppVideoCaptureSupportService = CaptureTool.Application.Abstractions.Capture.IVideoCaptureSupportService;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsScreenRecorderTests
{
    [TestMethod]
    public void StartRecording_ShouldRejectUnsupportedDevice_BeforeCreatingSession()
    {
        var captureService = new Mock<IVideoCaptureService>();
        var supportService = new Mock<AppVideoCaptureSupportService>();
        supportService
            .Setup(service => service.GetSupportStatus())
            .Returns(VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture));
        var recorder = new WindowsScreenRecorder(captureService.Object, supportService.Object, _ => { });

        Action act = () => recorder.StartRecording(CreateOptions());

        act.Should().Throw<AppVideoCaptureNotSupportedException>()
            .Which.Reason.Should().Be(VideoCaptureUnsupportedReason.GraphicsCapture);
        captureService.Verify(
            service => service.CreateSession(It.IsAny<VideoCaptureOptions>()),
            Times.Never);
    }

    [TestMethod]
    public void StartRecording_ShouldRejectUnavailableTarget_BeforeCreatingSession()
    {
        var captureService = new Mock<IVideoCaptureService>();
        var supportService = CreateSupportedService();
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            supportService.Object,
            _ => throw new VideoCaptureTargetUnavailableException());

        Action act = () => recorder.StartRecording(CreateOptions());

        act.Should().Throw<VideoCaptureTargetUnavailableException>();
        captureService.Verify(
            service => service.CreateSession(It.IsAny<VideoCaptureOptions>()),
            Times.Never);
    }

    [TestMethod]
    public void StartRecording_ShouldTranslateCaptureKitSupportFailure()
    {
        var session = new Mock<IVideoCaptureSession>();
        session
            .Setup(value => value.Start())
            .Throws(new CaptureKit.Abstractions.VideoCaptureNotSupportedException(
                VideoCaptureSupportResult.Unsupported(
                    VideoCaptureSupportReason.GraphicsCaptureNotSupported)));
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });

        Action act = () => recorder.StartRecording(CreateOptions());

        act.Should().Throw<AppVideoCaptureNotSupportedException>()
            .Which.Reason.Should().Be(VideoCaptureUnsupportedReason.GraphicsCapture);
        session.Verify(value => value.Dispose(), Times.Once);
    }

    [TestMethod]
    public void StartRecording_ShouldExposeCaptureKitStatusAndHResultForLocalDiagnostics()
    {
        const int accessDenied = unchecked((int)0x80070005);
        var session = new Mock<IVideoCaptureSession>();
        session
            .Setup(value => value.Start())
            .Throws(new CaptureRecorderException(CaptureRecorderStatus.StartFailed, accessDenied));
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });

        Action act = () => recorder.StartRecording(CreateOptions());

        InvalidOperationException exception = act.Should()
            .Throw<InvalidOperationException>()
            .Which;
        exception.Message.Should().Contain("CaptureKit status StartFailed");
        exception.Message.Should().Contain("HRESULT 0x80070005");
        exception.InnerException.Should().BeOfType<CaptureRecorderException>();
        session.Verify(value => value.Dispose(), Times.Once);
    }

    [TestMethod]
    public void RecordingStarted_ShouldOnlyBeRaisedByVideoFrames()
    {
        var session = new Mock<IVideoCaptureSession>();
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        int raisedCount = 0;
        recorder.RecordingStarted += (_, _) => raisedCount++;
        recorder.StartRecording(CreateOptions());

        session.Raise(
            value => value.AudioSampleCaptured += null,
            new AudioSampleCapturedEventArgs(new AudioSampleData()));
        session.Raise(
            value => value.FrameCaptured += null,
            new VideoFrameCapturedEventArgs(new VideoFrameData()));

        raisedCount.Should().Be(1);
        recorder.StopRecording();
    }

    [TestMethod]
    public void VideoCallback_ShouldContainRecordingStartedSubscriberExceptions()
    {
        var session = new Mock<IVideoCaptureSession>();
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        recorder.RecordingStarted += (_, _) => throw new InvalidOperationException("subscriber failed");
        recorder.StartRecording(CreateOptions());

        Action callback = () => session.Raise(
            value => value.FrameCaptured += null,
            new VideoFrameCapturedEventArgs(new VideoFrameData()));

        callback.Should().NotThrow();
        recorder.StopRecording();
    }

    [TestMethod]
    public void VideoCallback_FromDisposedSession_ShouldNotStartLaterRetry()
    {
        EventHandler<VideoFrameCapturedEventArgs>? staleCallback = null;
        var firstSession = new Mock<IVideoCaptureSession>();
        firstSession
            .SetupAdd(session => session.FrameCaptured += It.IsAny<EventHandler<VideoFrameCapturedEventArgs>>())
            .Callback<EventHandler<VideoFrameCapturedEventArgs>>(handler => staleCallback = handler);
        var secondSession = new Mock<IVideoCaptureSession>();
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .SetupSequence(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(firstSession.Object)
            .Returns(secondSession.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        int raisedCount = 0;
        recorder.RecordingStarted += (_, _) => raisedCount++;

        recorder.StartRecording(CreateOptions());
        recorder.StopRecording();
        recorder.StartRecording(CreateOptions());
        staleCallback!(
            firstSession.Object,
            new VideoFrameCapturedEventArgs(new VideoFrameData()));

        raisedCount.Should().Be(0);
        secondSession.Raise(
            value => value.FrameCaptured += null,
            new VideoFrameCapturedEventArgs(new VideoFrameData()));
        raisedCount.Should().Be(1);
        recorder.StopRecording();
    }

    [TestMethod]
    public void VideoCallback_ReentrantStop_ShouldBeContainedWithoutStoppingSession()
    {
        var session = new Mock<IVideoCaptureSession>();
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        recorder.RecordingStarted += (_, _) => recorder.StopRecording();
        recorder.StartRecording(CreateOptions());

        Action callback = () => session.Raise(
            value => value.FrameCaptured += null,
            new VideoFrameCapturedEventArgs(new VideoFrameData()));

        callback.Should().NotThrow();
        session.Verify(value => value.Stop(), Times.Never);
        recorder.StopRecording();
        session.Verify(value => value.Stop(), Times.Once);
    }

    [TestMethod]
    public void SetAudioCaptureEnabled_WhenRecordingStartedMuted_ShouldForwardUnmuteToActiveSession()
    {
        var session = new Mock<IVideoCaptureSession>();
        VideoCaptureOptions? capturedOptions = null;
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Callback<VideoCaptureOptions>(options => capturedOptions = options)
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        CaptureRecordingOptions options = CreateOptions() with { CaptureAudio = false };

        recorder.StartRecording(options);
        recorder.SetAudioCaptureEnabled(true);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.CaptureAudio.Should().BeFalse();
        session.Verify(value => value.SetAudioCaptureEnabled(true), Times.Once);
        recorder.StopRecording();
    }

    [TestMethod]
    public void DesktopAudioVolume_ShouldMapAtStartupAndRemainMutable()
    {
        var session = new Mock<IVideoCaptureSession>();
        VideoCaptureOptions? capturedOptions = null;
        var captureService = new Mock<IVideoCaptureService>();
        captureService
            .Setup(service => service.CreateSession(It.IsAny<VideoCaptureOptions>()))
            .Callback<VideoCaptureOptions>(options => capturedOptions = options)
            .Returns(session.Object);
        var recorder = new WindowsScreenRecorder(
            captureService.Object,
            CreateSupportedService().Object,
            _ => { });
        CaptureRecordingOptions options = CreateOptions() with { DesktopAudioVolumePercentage = 37 };

        recorder.StartRecording(options);
        recorder.SetDesktopAudioVolume(64);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.SystemAudioVolumePercentage.Should().Be(37);
        session.Verify(value => value.SetSystemAudioVolume(64), Times.Once);
        recorder.StopRecording();
    }

    private static Mock<AppVideoCaptureSupportService> CreateSupportedService()
    {
        var supportService = new Mock<AppVideoCaptureSupportService>();
        supportService
            .Setup(service => service.GetSupportStatus())
            .Returns(VideoCaptureSupportStatus.Supported);
        return supportService;
    }

    private static CaptureRecordingOptions CreateOptions()
        => new(
            CaptureRecordingTarget.Monitor((nint)1),
            @"C:\Temp\capture.mp4");

}
