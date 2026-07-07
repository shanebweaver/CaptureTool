using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    private readonly CaptureKit.Abstractions.IVideoCaptureService _videoCaptureService;
    private CaptureKit.Abstractions.IVideoCaptureSession? _session;
    private VideoFrameCallback? _videoFrameCallback;
    private AudioSampleCallback? _audioSampleCallback;

    public WindowsScreenRecorder(CaptureKit.Abstractions.IVideoCaptureService videoCaptureService)
    {
        _videoCaptureService = videoCaptureService;
    }

    public CaptureRecorderResult StartRecording(CaptureRecordingOptions options)
    {
        if (_session is not null)
        {
            return new CaptureRecorderResult(CaptureRecorderStatus.InvalidState, 0);
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

            return Success();
        }
        catch (Exception ex)
        {
            DisposeSession();
            return Failure(CaptureRecorderStatus.StartFailed, ex);
        }
    }

    public CaptureRecorderResult StopRecording()
    {
        if (_session is null)
        {
            return new CaptureRecorderResult(CaptureRecorderStatus.NoActiveSession, 0);
        }

        try
        {
            _session.Stop();
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(CaptureRecorderStatus.InvalidState, ex);
        }
        finally
        {
            DisposeSession();
        }
    }

    public CaptureRecorderResult PauseRecording()
        => ExecuteOnSession(session => session.Pause());

    public CaptureRecorderResult ResumeRecording()
        => ExecuteOnSession(session => session.Resume());

    public CaptureRecorderResult SetAudioCaptureEnabled(bool enabled)
        => ExecuteOnSession(session => session.SetAudioCaptureEnabled(enabled));

    public CaptureRecorderResult SetAudioInputSource(string? sourceId)
        => ExecuteOnSession(session => session.SetAudioInputSource(sourceId));

    public CaptureRecorderResult SetAudioInputVolume(int volumePercentage)
        => ExecuteOnSession(session => session.SetAudioInputVolume(volumePercentage));

    public CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback? callback)
    {
        _videoFrameCallback = callback;
        return Success();
    }

    public CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback? callback)
    {
        _audioSampleCallback = callback;
        return Success();
    }

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

    private CaptureRecorderResult ExecuteOnSession(Action<CaptureKit.Abstractions.IVideoCaptureSession> action)
    {
        if (_session is null)
        {
            return new CaptureRecorderResult(CaptureRecorderStatus.NoActiveSession, 0);
        }

        try
        {
            action(_session);
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(CaptureRecorderStatus.InvalidState, ex);
        }
    }

    private void OnFrameCaptured(object? sender, CaptureKit.Abstractions.VideoFrameCapturedEventArgs e)
    {
        VideoFrameCallback? callback = _videoFrameCallback;
        if (callback is null)
        {
            return;
        }

        CaptureKit.Abstractions.VideoFrameData frame = e.FrameData;
        var data = new VideoFrameData
        {
            pTexture = frame.TexturePointer,
            Timestamp = frame.Timestamp,
            Width = frame.Width,
            Height = frame.Height
        };

        callback(ref data);
    }

    private void OnAudioSampleCaptured(object? sender, CaptureKit.Abstractions.AudioSampleCapturedEventArgs e)
    {
        AudioSampleCallback? callback = _audioSampleCallback;
        if (callback is null)
        {
            return;
        }

        CaptureKit.Abstractions.AudioSampleData sample = e.SampleData;
        var data = new AudioSampleData
        {
            pData = sample.DataPointer,
            NumFrames = sample.NumFrames,
            Timestamp = sample.Timestamp,
            SampleRate = sample.SampleRate,
            Channels = sample.Channels,
            BitsPerSample = sample.BitsPerSample
        };

        callback(ref data);
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

    private static CaptureRecorderResult Success()
        => new(CaptureRecorderStatus.Success, 0);

    private static CaptureRecorderResult Failure(CaptureRecorderStatus status, Exception exception)
        => new(status, exception.HResult);
}
