namespace CaptureTool.Application.Abstractions.Capture;

public interface IScreenRecorder
{
    event EventHandler? RecordingStarted;

    void StartRecording(CaptureRecordingOptions options);
    void StopRecording();
    void PauseRecording();
    void ResumeRecording();
    void SetAudioCaptureEnabled(bool enabled);
    void SetDesktopAudioVolume(int volumePercentage);
    void SetAudioInputSource(string? sourceId);
    void SetAudioInputVolume(int volumePercentage);
}

public readonly record struct CaptureRecordingOptions(
    CaptureRecordingTarget Target,
    string OutputPath,
    bool CaptureAudio = false,
    uint FrameRate = 30,
    uint VideoBitrate = 5_000_000,
    uint AudioBitrate = 128_000,
    string? AudioInputSourceId = null,
    int AudioInputVolumePercentage = 100,
    int DesktopAudioVolumePercentage = 100);

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
