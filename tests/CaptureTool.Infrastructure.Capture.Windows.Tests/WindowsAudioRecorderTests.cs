using CaptureKit.Abstractions;
using CaptureTool.Application.Abstractions.Capture;
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

        recorder.StartCapture(new AudioCaptureRecordingOptions(
            @"C:\Temp\capture.wav",
            CaptureDesktopAudio: false,
            AudioInputSourceId: "microphone",
            AudioInputVolumePercentage: 37));

        capturedOptions.Should().NotBeNull();
        capturedOptions!.CaptureAudio.Should().BeFalse();
        capturedOptions.AudioInputSourceId.Should().Be("microphone");
        capturedOptions.AudioInputVolumePercentage.Should().Be(37);
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

        recorder.StartCapture(new AudioCaptureRecordingOptions(@"C:\Temp\capture.wav", true, "microphone"));
        recorder.SetAudioInputSource("microphone");
        recorder.SetAudioInputSource(null);

        session.Verify(captureSession => captureSession.SetAudioInputSource("microphone"), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioInputSource(null), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioCaptureEnabled(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public void SetDesktopAudioWhileRecording_ShouldOnlyUpdateDesktopAudio()
    {
        Mock<IAudioCaptureSession> session = new();
        Mock<IAudioCaptureService> service = new();
        service
            .Setup(captureService => captureService.CreateSession(It.IsAny<AudioCaptureOptions>()))
            .Returns(session.Object);
        WindowsAudioRecorder recorder = new(service.Object);

        recorder.StartCapture(new AudioCaptureRecordingOptions(@"C:\Temp\capture.wav", true));
        recorder.SetDesktopAudioEnabled(false);

        session.Verify(captureSession => captureSession.SetAudioCaptureEnabled(false), Times.Once);
        session.Verify(captureSession => captureSession.SetAudioInputSource(It.IsAny<string?>()), Times.Never);
    }
}
