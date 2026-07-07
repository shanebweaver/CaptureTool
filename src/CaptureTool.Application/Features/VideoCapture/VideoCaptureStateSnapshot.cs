namespace CaptureTool.Application.Features.VideoCapture;

internal readonly record struct VideoCaptureStateSnapshot(
    VideoCaptureStatus Status,
    VideoCaptureAudioSettings AudioSettings)
{
    public bool IsRecording => Status is VideoCaptureStatus.Recording or VideoCaptureStatus.Paused;
    public bool IsFinalizing => Status == VideoCaptureStatus.Finalizing;
    public bool IsPaused => Status == VideoCaptureStatus.Paused;
    public bool IsDesktopAudioEnabled => AudioSettings.IsDesktopAudioEnabled;
    public bool IsAudioInputMuted => AudioSettings.IsAudioInputMuted;
    public int AudioInputVolumePercentage => AudioSettings.AudioInputVolumePercentage;
    public string? SelectedAudioInputSourceId => AudioSettings.SelectedAudioInputSourceId;

    public static VideoCaptureStateSnapshot Idle(VideoCaptureAudioSettings audioSettings)
        => new(VideoCaptureStatus.Idle, audioSettings);

    public static VideoCaptureStateSnapshot FromSession(VideoCaptureSession session)
        => new(session.Status, session.AudioSettings);
}
