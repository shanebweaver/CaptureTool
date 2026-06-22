using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.AudioCapture;

public sealed class AudioCaptureHandler : IAudioCaptureHandler
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IStorageService _storageService;

    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<IAudioFile>? NewAudioCaptured;

    public bool IsRecording => CaptureState is AudioCaptureState.Recording or AudioCaptureState.Paused;
    public bool IsPaused => CaptureState == AudioCaptureState.Paused;
    public bool IsMuted { get; private set; }
    public bool IsDesktopAudioEnabled { get; private set; } = true;

    public AudioCaptureState CaptureState { get; private set; }

    public AudioCaptureHandler(
        IAudioRecorder audioRecorder,
        IStorageService storageService)
    {
        _audioRecorder = audioRecorder;
        _storageService = storageService;
    }

    public void PauseCapture()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        if (IsPaused)
        {
            _audioRecorder.Resume();
            UpdateCaptureState(AudioCaptureState.Recording);
            return;
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

        string tempAudioPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            GetNewCaptureFileName());

        _audioRecorder.StartCapture(tempAudioPath);

        UpdateCaptureState(AudioCaptureState.Recording);
    }

    public IAudioFile StopCapture()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        IAudioFile audioFile = _audioRecorder.StopCapture();

        UpdateCaptureState(AudioCaptureState.Stopped);
        NewAudioCaptured?.Invoke(this, audioFile);

        return audioFile;
    }

    public void ToggleLocalAudio()
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

    private void UpdateCaptureState(AudioCaptureState newState)
    {
        if (CaptureState != newState)
        {
            CaptureState = newState;
            CaptureStateChanged?.Invoke(this, newState);
        }
    }

    private static string GetNewCaptureFileName()
    {
        DateTime timestamp = DateTime.Now;
        return $"Capture_{timestamp:yyyy-MM-dd}_{timestamp:FFFFF}.wav";
    }
}
