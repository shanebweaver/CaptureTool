using CaptureTool.Application.Abstractions.Capture;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    [ThreadStatic]
    private static int s_callbackDepth;

    private readonly CaptureKit.Abstractions.IVideoCaptureService _videoCaptureService;
    private readonly IVideoCaptureSupportService _videoCaptureSupportService;
    private readonly Action<CaptureRecordingTarget> _validateTarget;
    private readonly Lock _sessionLock = new();
    private CaptureKit.Abstractions.IVideoCaptureSession? _session;

    public WindowsScreenRecorder(
        CaptureKit.Abstractions.IVideoCaptureService videoCaptureService,
        IVideoCaptureSupportService videoCaptureSupportService)
        : this(videoCaptureService, videoCaptureSupportService, ValidateTarget)
    {
    }

    internal WindowsScreenRecorder(
        CaptureKit.Abstractions.IVideoCaptureService videoCaptureService,
        IVideoCaptureSupportService videoCaptureSupportService,
        Action<CaptureRecordingTarget> validateTarget)
    {
        _videoCaptureService = videoCaptureService;
        _videoCaptureSupportService = videoCaptureSupportService;
        _validateTarget = validateTarget;
    }

    public event EventHandler? RecordingStarted;

    public void StartRecording(CaptureRecordingOptions options)
    {
        ThrowIfInsideCaptureCallback();
        lock (_sessionLock)
        {
            VideoCaptureSupportStatus supportStatus = _videoCaptureSupportService.GetSupportStatus();
            if (!supportStatus.IsSupported)
            {
                throw new VideoCaptureNotSupportedException(supportStatus.UnsupportedReason);
            }

            _validateTarget(options.Target);

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
                _session.Start();
            }
            catch (CaptureKit.Abstractions.VideoCaptureNotSupportedException ex)
            {
                DisposeSession();
                throw new VideoCaptureNotSupportedException(MapUnsupportedReason(ex.Reason));
            }
            catch (CaptureKit.Abstractions.CaptureRecorderException ex)
            {
                DisposeSession();
                throw new InvalidOperationException(
                    $"Failed to start video recording (CaptureKit status {ex.Status}, HRESULT 0x{ex.ErrorCode:X8}).",
                    ex);
            }
            catch (Exception ex)
            {
                DisposeSession();
                throw new InvalidOperationException("Failed to start video recording.", ex);
            }
        }
    }

    public void StopRecording()
    {
        ThrowIfInsideCaptureCallback();
        lock (_sessionLock)
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

    private static VideoCaptureUnsupportedReason MapUnsupportedReason(
        CaptureKit.Abstractions.VideoCaptureSupportReason reason)
    {
        return reason switch
        {
            CaptureKit.Abstractions.VideoCaptureSupportReason.UnsupportedOperatingSystem =>
                VideoCaptureUnsupportedReason.OperatingSystem,
            _ => VideoCaptureUnsupportedReason.GraphicsCapture
        };
    }

    private static void ValidateTarget(CaptureRecordingTarget target)
    {
        bool isAvailable = target.Kind switch
        {
            CaptureRecordingTargetKind.Monitor => IsMonitorAvailable(target.MonitorHandle),
            CaptureRecordingTargetKind.Window => target.WindowHandle != 0 && IsWindow(target.WindowHandle),
            CaptureRecordingTargetKind.Rectangle =>
                IsMonitorAvailable(target.MonitorHandle) && target.Width > 0 && target.Height > 0,
            _ => false
        };

        if (!isAvailable)
        {
            throw new VideoCaptureTargetUnavailableException();
        }
    }

    private static bool IsMonitorAvailable(nint monitorHandle)
    {
        if (monitorHandle == 0)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };

        return GetMonitorInfo(monitorHandle, ref monitorInfo);
    }

    private void ExecuteOnSession(Action<CaptureKit.Abstractions.IVideoCaptureSession> action, string operation)
    {
        ThrowIfInsideCaptureCallback();
        lock (_sessionLock)
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
    }

    private void OnFrameCaptured(object? sender, CaptureKit.Abstractions.VideoFrameCapturedEventArgs e)
    {
        // Event unsubscription cannot retract a callback that already copied its
        // invocation list. Do not let a late frame from a disposed session satisfy
        // a later retry's first-frame handshake.
        if (!ReferenceEquals(sender, Volatile.Read(ref _session)))
        {
            return;
        }

        s_callbackDepth++;
        try
        {
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // This handler is invoked by a reverse-P/Invoke callback. Subscriber
            // failures must never cross back into CaptureKit's native worker thread.
        }
        finally
        {
            s_callbackDepth--;
        }
    }

    private static void ThrowIfInsideCaptureCallback()
    {
        if (s_callbackDepth != 0)
        {
            throw new InvalidOperationException(
                "Recorder control cannot be invoked synchronously from a capture callback.");
        }
    }

    private void DisposeSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.FrameCaptured -= OnFrameCaptured;
        _session.Dispose();
        _session = null;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint windowHandle);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
