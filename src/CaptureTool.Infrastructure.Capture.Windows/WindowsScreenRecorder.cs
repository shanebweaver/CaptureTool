using CaptureTool.Application.Abstractions.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    private readonly CaptureKit.Abstractions.IVideoCaptureService _videoCaptureService;
    private CaptureKit.Abstractions.IVideoCaptureSession? _session;

    public WindowsScreenRecorder(CaptureKit.Abstractions.IVideoCaptureService videoCaptureService)
    {
        _videoCaptureService = videoCaptureService;
    }

    public event EventHandler? RecordingStarted;

    public void StartRecording(CaptureRecordingOptions options)
    {
        if (_session is not null)
        {
            throw new InvalidOperationException("A video capture session is already active.");
        }

        try
        {
            var captureOptions = new CaptureKit.Abstractions.VideoCaptureOptions(
                MapTarget(options.Target),
                options.OutputPath,
                options.CaptureAudio,
                options.FrameRate,
                options.VideoBitrate,
                options.AudioBitrate,
                options.AudioInputSourceId,
                options.AudioInputVolumePercentage);

            _session = _videoCaptureService.CreateSession(captureOptions);
            _session.FrameCaptured += OnFrameCaptured;
            _session.AudioSampleCaptured += OnAudioSampleCaptured;
            _session.Start();
        }
        catch (Exception ex)
        {
            DisposeSession();
            throw new InvalidOperationException("Failed to start video recording.", ex);
        }
    }

    public void StopRecording()
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No video capture session is active.");
        }

        try
        {
            _session.Stop();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to stop video recording.", ex);
        }
        finally
        {
            DisposeSession();
        }
    }

    public void PauseRecording()
        => ExecuteOnSession(session => session.Pause(), "pause video recording");

    public void ResumeRecording()
        => ExecuteOnSession(session => session.Resume(), "resume video recording");

    public void SetAudioCaptureEnabled(bool enabled)
        => ExecuteOnSession(session => session.SetAudioCaptureEnabled(enabled), "update audio capture state");

    public void SetAudioInputSource(string? sourceId)
        => ExecuteOnSession(session => session.SetAudioInputSource(sourceId), "update audio input source");

    public void SetAudioInputVolume(int volumePercentage)
        => ExecuteOnSession(session => session.SetAudioInputVolume(volumePercentage), "update audio input volume");

    private static CaptureKit.Abstractions.CaptureTarget MapTarget(CaptureRecordingTarget target)
    {
        return target.Kind switch
        {
            CaptureRecordingTargetKind.Monitor => CaptureKit.Abstractions.CaptureTarget.Monitor(target.MonitorHandle),
            CaptureRecordingTargetKind.Window => CaptureKit.Abstractions.CaptureTarget.Window(target.WindowHandle),
            CaptureRecordingTargetKind.Rectangle => CaptureKit.Abstractions.CaptureTarget.Rectangle(
                target.MonitorHandle,
                target.Left,
                target.Top,
                target.Width,
                target.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unsupported recording target.")
        };
    }

    private void ExecuteOnSession(Action<CaptureKit.Abstractions.IVideoCaptureSession> action, string operation)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No video capture session is active.");
        }

        try
        {
            action(_session);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to {operation}.", ex);
        }
    }

    private void OnFrameCaptured(object? sender, CaptureKit.Abstractions.VideoFrameCapturedEventArgs e)
    {
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    private void OnAudioSampleCaptured(object? sender, CaptureKit.Abstractions.AudioSampleCapturedEventArgs e)
    {
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.FrameCaptured -= OnFrameCaptured;
        _session.AudioSampleCaptured -= OnAudioSampleCaptured;
        _session.Dispose();
        _session = null;
    }
}
