using CaptureKit.Abstractions;
using FluentAssertions;
using Moq;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsAudioRecorderTests
{
    [TestMethod]
    public void StartCapture_ShouldPassDesktopAudioAndInputSourceSeparately()
    {
        AudioCaptureOptions? capturedOptions = null;
        Mock<IAudioCaptureSession> session = new();
        Mock<IAudioCaptureService> service = new();
        service
            .Setup(captureService => captureService.CreateSession(It.IsAny<AudioCaptureOptions>()))
            .Callback<AudioCaptureOptions>(options => capturedOptions = options)
            .Returns(session.Object);
        WindowsAudioRecorder recorder = new(service.Object);

        recorder.ToggleDesktopAudio();
        recorder.SetAudioInputSource("microphone");
        recorder.StartCapture(@"C:\Temp\capture.wav");

        capturedOptions.Should().NotBeNull();
        capturedOptions!.CaptureAudio.Should().BeFalse();
        capturedOptions.AudioInputSourceId.Should().Be("microphone");
    }

    [TestMethod]
    public void MicrophoneChangesWhileRecording_ShouldNotToggleDesktopAudio()
    {
        Mock<IAudioCaptureSession> session = new();
        Mock<IAudioCaptureService> service = new();
        service
            .Setup(captureService => captureService.CreateSession(It.IsAny<AudioCaptureOptions>()))
            .Returns(session.Object);
        WindowsAudioRecorder recorder = new(service.Object);

        recorder.StartCapture(@"C:\Temp\capture.wav");
        recorder.SetAudioInputSource("microphone");
        recorder.ToggleMute();

        session.Verify(captureSession => captureSession.SetAudioInputSource("microphone"), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioInputSource(null), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioCaptureEnabled(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void ToggleDesktopAudioWhileRecording_ShouldOnlyUpdateDesktopAudio()
    {
        Mock<IAudioCaptureSession> session = new();
        Mock<IAudioCaptureService> service = new();
        service
            .Setup(captureService => captureService.CreateSession(It.IsAny<AudioCaptureOptions>()))
            .Returns(session.Object);
        WindowsAudioRecorder recorder = new(service.Object);

        recorder.StartCapture(@"C:\Temp\capture.wav");
        recorder.ToggleDesktopAudio();

        session.Verify(captureSession => captureSession.SetAudioCaptureEnabled(false), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioInputSource(It.IsAny<string?>()), Times.Never);
    }
}
