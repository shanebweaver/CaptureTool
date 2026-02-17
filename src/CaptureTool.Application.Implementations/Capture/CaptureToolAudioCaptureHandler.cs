using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Application.Implementations.Capture;

internal class CaptureToolAudioCaptureHandler : IAudioCaptureHandler
{
    private readonly IAudioRecorder _audioRecorder;

    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;

    public bool IsRecording => CaptureState == AudioCaptureState.Recording;
    public bool IsPaused => CaptureState == AudioCaptureState.Paused;
    public bool IsMuted { get; private set; }
    public bool IsDesktopAudioEnabled { get; private set; }

    public AudioCaptureState CaptureState { get; private set; }

    public CaptureToolAudioCaptureHandler(
        IAudioRecorder audioRecorder)
    {
        _audioRecorder = audioRecorder;
    }

    public void PauseCapture()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        _audioRecorder.Pause();

        UpdateCaptureState(AudioCaptureState.Paused);
    }

    public void StartCapture()
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Audio capture is already in progress.");
        }

        _audioRecorder.StartCapture();

        UpdateCaptureState(AudioCaptureState.Recording);
    }

    public IAudioFile StopCapture()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        _audioRecorder.StopCapture();

        UpdateCaptureState(AudioCaptureState.Stopped);

        throw new NotImplementedException();
    }

    public void ToggleDesktopAudio()
    {
        _audioRecorder.ToggleDesktopAudio();

        IsDesktopAudioEnabled = !IsDesktopAudioEnabled;
        DesktopAudioStateChanged?.Invoke(this, IsDesktopAudioEnabled);
    }

    public void ToggleMute()
    {
        _audioRecorder.ToggleMute();

        IsMuted = !IsMuted;
        MutedStateChanged?.Invoke(this, IsMuted);
    }

    protected void UpdateCaptureState(AudioCaptureState newState)
    {
        if (CaptureState != newState)
        {
            CaptureState = newState;
            CaptureStateChanged?.Invoke(this, newState);
        }
    }
}