using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.V2;

[TestClass]
[DoNotParallelize]
public sealed class CaptureV2ScreenRecorderAdapterTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        Environment.SetEnvironmentVariable("CAPTURETOOL_V2_FAKE_NATIVE_SESSION", "1");
    }

    [TestMethod]
    public void StartPauseToggleResumeStop_WithAudio_UsesV2Recorder()
    {
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool started = recorder.StartRecording(123, @"C:\Temp\capture-v2.mp4", captureAudio: true);
        recorder.ToggleAudioCapture(false);
        recorder.PauseRecording();
        recorder.ResumeRecording();
        recorder.StopRecording();

        started.Should().BeTrue();
    }

    [TestMethod]
    public void StartRecording_WhileAlreadyRecording_ReturnsFalse()
    {
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool first = recorder.StartRecording(123, @"C:\Temp\capture-v2.mp4");
        bool second = recorder.StartRecording(123, @"C:\Temp\capture-v2-second.mp4");

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [TestMethod]
    public void ToggleAudioCapture_WhenAudioWasNotArmed_DoesNotThrow()
    {
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool started = recorder.StartRecording(123, @"C:\Temp\capture-v2.mp4", captureAudio: false);
        Action toggle = () => recorder.ToggleAudioCapture(false);

        started.Should().BeTrue();
        toggle.Should().NotThrow();
    }

    [TestMethod]
    public void StartRecording_WhenNativeStartFails_DisposesPartialRecorderAndReturnsFalse()
    {
        using var recorder = new CaptureV2ScreenRecorderAdapter();

        bool started = recorder.StartRecording(123, string.Empty, captureAudio: true);
        bool retryStarted = recorder.StartRecording(123, @"C:\Temp\capture-v2-retry.mp4", captureAudio: true);

        started.Should().BeFalse();
        retryStarted.Should().BeTrue();
    }
}
