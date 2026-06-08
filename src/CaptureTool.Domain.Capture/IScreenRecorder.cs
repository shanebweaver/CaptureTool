using System.Runtime.InteropServices;

namespace CaptureTool.Domain.Capture;

public partial interface IScreenRecorder
{
    CaptureRecorderResult StartRecording(CaptureRecordingOptions options);
    CaptureRecorderResult StopRecording();
    CaptureRecorderResult PauseRecording();
    CaptureRecorderResult ResumeRecording();
    CaptureRecorderResult SetAudioCaptureEnabled(bool enabled);
    CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback? callback);
    CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback? callback);
}

public readonly record struct CaptureRecordingOptions(
    CaptureRecordingTarget Target,
    string OutputPath,
    bool CaptureAudio = false,
    uint FrameRate = 30,
    uint VideoBitrate = 5_000_000,
    uint AudioBitrate = 128_000);

public readonly record struct CaptureRecordingTarget(
    CaptureRecordingTargetKind Kind,
    nint MonitorHandle = 0,
    nint WindowHandle = 0,
    int Left = 0,
    int Top = 0,
    int Width = 0,
    int Height = 0)
{
    public static CaptureRecordingTarget Monitor(nint monitorHandle)
        => new(CaptureRecordingTargetKind.Monitor, MonitorHandle: monitorHandle);

    public static CaptureRecordingTarget Window(nint windowHandle)
        => new(CaptureRecordingTargetKind.Window, WindowHandle: windowHandle);

    public static CaptureRecordingTarget Rectangle(nint monitorHandle, int left, int top, int width, int height)
        => new(CaptureRecordingTargetKind.Rectangle, monitorHandle, Left: left, Top: top, Width: width, Height: height);
}

public enum CaptureRecordingTargetKind
{
    Monitor = 0,
    Window = 1,
    Rectangle = 2
}

public enum CaptureRecorderStatus
{
    Success = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    StartFailed = 3,
    NoActiveSession = 4
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CaptureRecorderResult
{
    public readonly CaptureRecorderStatus Status;
    public readonly int HResult;

    public CaptureRecorderResult(CaptureRecorderStatus status, int hResult)
    {
        Status = status;
        HResult = hResult;
    }

    public bool IsSuccess => Status == CaptureRecorderStatus.Success;

    public void EnsureSuccess()
    {
        if (!IsSuccess)
        {
            Marshal.ThrowExceptionForHR(HResult);
            throw new InvalidOperationException($"Capture recorder operation failed with status {Status}.");
        }
    }
}

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void VideoFrameCallback(ref VideoFrameData frameData);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void AudioSampleCallback(ref AudioSampleData sampleData);

