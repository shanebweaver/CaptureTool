using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Audio;

internal readonly record struct AudioCaptureStateSnapshot(
    AudioCaptureState CaptureState,
    AudioCaptureSettings Settings)
{
    public bool IsRecording => CaptureState is AudioCaptureState.Recording or AudioCaptureState.Paused;
    public bool IsPaused => CaptureState == AudioCaptureState.Paused;
    public bool IsMuted => Settings.IsMuted;
    public bool IsDesktopAudioEnabled => Settings.IsDesktopAudioEnabled;
    public string? SelectedAudioInputSourceId => Settings.SelectedAudioInputSourceId;

    public static AudioCaptureStateSnapshot Stopped(AudioCaptureSettings settings)
        => new(AudioCaptureState.Stopped, settings);

    public static AudioCaptureStateSnapshot FromSession(AudioCaptureSession session)
        => new(session.CaptureState, session.Settings);
}
