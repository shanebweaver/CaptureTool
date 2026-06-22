using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Infrastructure.Capture.Windows;

public class WindowsAudioRecorder : IAudioRecorder
{
    private string? _outputPath;
    private bool _isMuted;
    private bool _isDesktopAudioEnabled = true;

    public void Pause()
    {
        CaptureInterop.PauseAudioRecording().EnsureSuccess();
    }

    public void Resume()
    {
        CaptureInterop.ResumeAudioRecording().EnsureSuccess();
    }

    public void StartCapture(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Audio output path is required.", nameof(outputPath));
        }

        _outputPath = outputPath;

        var options = new NativeAudioRecordingOptions(
            outputPath,
            ShouldCaptureAudio(),
            null,
            100);

        try
        {
            CaptureInterop.StartAudioRecording(in options).EnsureSuccess();
        }
        catch
        {
            _outputPath = null;
            throw;
        }
    }

    public IAudioFile StopCapture()
    {
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            throw new InvalidOperationException("Cannot stop, no audio is recording.");
        }

        try
        {
            CaptureInterop.StopAudioRecording().EnsureSuccess();
            return new AudioFile(_outputPath);
        }
        finally
        {
            _outputPath = null;
        }
    }

    public void ToggleDesktopAudio()
    {
        _isDesktopAudioEnabled = !_isDesktopAudioEnabled;
        if (!string.IsNullOrWhiteSpace(_outputPath))
        {
            CaptureInterop.SetAudioRecordingEnabled(ShouldCaptureAudio() ? 1u : 0u).EnsureSuccess();
        }
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        if (!string.IsNullOrWhiteSpace(_outputPath))
        {
            CaptureInterop.SetAudioRecordingEnabled(ShouldCaptureAudio() ? 1u : 0u).EnsureSuccess();
        }
    }

    private bool ShouldCaptureAudio()
        => !_isMuted && _isDesktopAudioEnabled;
}
