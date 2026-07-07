using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.AudioCapture;

internal sealed class AudioCaptureSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public string TempAudioPath { get; }
    public AudioCaptureState CaptureState { get; private set; } = AudioCaptureState.Recording;
    public AudioCaptureSettings Settings { get; private set; }

    public AudioCaptureSession(string tempAudioPath, AudioCaptureSettings settings)
    {
        TempAudioPath = tempAudioPath;
        Settings = settings;
    }

    public bool IsRecording => CaptureState is AudioCaptureState.Recording or AudioCaptureState.Paused;
    public bool IsPaused => CaptureState == AudioCaptureState.Paused;

    public AudioCaptureState PauseOrResume()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        CaptureState = IsPaused
            ? AudioCaptureState.Recording
            : AudioCaptureState.Paused;

        return CaptureState;
    }

    public void SetSettings(AudioCaptureSettings settings)
    {
        Settings = settings;
    }
}
