using CaptureKit.Abstractions;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Infrastructure.Capture.Windows;

public class WindowsAudioRecorder : IAudioRecorder
{
    private readonly IAudioCaptureService _audioCaptureService;
    private IAudioCaptureSession? _session;
    private string? _outputPath;
    private bool _isMuted;
    private bool _isDesktopAudioEnabled = true;
    private string? _audioInputSourceId;

    public WindowsAudioRecorder(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;
    }

    public void Pause()
    {
        GetActiveSession().Pause();
    }

    public void Resume()
    {
        GetActiveSession().Resume();
    }

    public void StartCapture(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Audio output path is required.", nameof(outputPath));
        }

        _outputPath = outputPath;

        var options = new AudioCaptureOptions(
            outputPath,
            ShouldCaptureAudio(),
            GetActiveAudioInputSourceId(),
            100);

        try
        {
            _session = _audioCaptureService.CreateSession(options);
            _session.Start();
        }
        catch
        {
            _outputPath = null;
            _session?.Dispose();
            _session = null;
            throw;
        }
    }

    public AudioFile StopCapture()
    {
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            throw new InvalidOperationException("Cannot stop, no audio is recording.");
        }

        try
        {
            GetActiveSession().Stop();
            return new AudioFile(_outputPath);
        }
        finally
        {
            _outputPath = null;
            _session?.Dispose();
            _session = null;
        }
    }

    public void ToggleDesktopAudio()
    {
        _isDesktopAudioEnabled = !_isDesktopAudioEnabled;
        if (!string.IsNullOrWhiteSpace(_outputPath))
        {
            GetActiveSession().SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    public void SetAudioInputSource(string? sourceId)
    {
        _audioInputSourceId = string.IsNullOrWhiteSpace(sourceId)
            ? null
            : sourceId;

        if (!string.IsNullOrWhiteSpace(_outputPath))
        {
            IAudioCaptureSession session = GetActiveSession();
            session.SetAudioInputSource(GetActiveAudioInputSourceId());
            session.SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        if (!string.IsNullOrWhiteSpace(_outputPath))
        {
            IAudioCaptureSession session = GetActiveSession();
            session.SetAudioInputSource(GetActiveAudioInputSourceId());
            session.SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    private string? GetActiveAudioInputSourceId()
        => _isMuted ? null : _audioInputSourceId;

    private bool ShouldCaptureAudio()
        => _isDesktopAudioEnabled || !string.IsNullOrWhiteSpace(GetActiveAudioInputSourceId());

    private IAudioCaptureSession GetActiveSession()
        => _session ?? throw new InvalidOperationException("No audio capture session is active.");
}
