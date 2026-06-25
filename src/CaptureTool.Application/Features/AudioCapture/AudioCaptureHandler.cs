using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using System.Threading;

namespace CaptureTool.Application.Features.AudioCapture;

public sealed class AudioCaptureHandler : IAudioCaptureHandler
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IStorageService _storageService;
    private int _hasObservedRecordingStart;
    private int _hasPendingRecordingStart;
    private AudioSampleCallback? _audioSampleCallback;

    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler? RecordingStarted;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<IAudioFile>? NewAudioCaptured;

    public bool IsRecording => CaptureState is AudioCaptureState.Recording or AudioCaptureState.Paused;
    public bool IsPaused => CaptureState == AudioCaptureState.Paused;
    public bool IsMuted { get; private set; }
    public bool IsDesktopAudioEnabled { get; private set; } = true;
    public string? SelectedAudioInputSourceId { get; private set; }

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

        try
        {
            RegisterRecordingStartedCallback();
            _audioRecorder.StartCapture(tempAudioPath);
        }
        catch
        {
            ClearRecordingStartedCallback();
            throw;
        }

        UpdateCaptureState(AudioCaptureState.Recording);
        RaisePendingRecordingStarted();
    }

    public IAudioFile StopCapture()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Audio capture is not in progress.");
        }

        ClearRecordingStartedCallback();
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

    public void SelectAudioInputSource(string? sourceId)
    {
        SelectedAudioInputSourceId = string.IsNullOrWhiteSpace(sourceId)
            ? null
            : sourceId;

        _audioRecorder.SetAudioInputSource(SelectedAudioInputSourceId);
    }

    public void ToggleMute()
    {
        _audioRecorder.ToggleMute();

        IsMuted = !IsMuted;
        MutedStateChanged?.Invoke(this, IsMuted);
    }

    private void RegisterRecordingStartedCallback()
    {
        _hasObservedRecordingStart = 0;
        _hasPendingRecordingStart = 0;
        _audioSampleCallback = OnAudioSampleCaptured;
        _audioRecorder.RegisterAudioSampleCallback(_audioSampleCallback);
    }

    private void ClearRecordingStartedCallback()
    {
        _audioRecorder.RegisterAudioSampleCallback(null);
        _audioSampleCallback = null;
        _hasPendingRecordingStart = 0;
    }

    private void OnAudioSampleCaptured(ref AudioSampleData sampleData)
    {
        if (Interlocked.Exchange(ref _hasObservedRecordingStart, 1) != 0)
        {
            return;
        }

        if (CaptureState == AudioCaptureState.Recording)
        {
            RecordingStarted?.Invoke(this, EventArgs.Empty);
            return;
        }

        Interlocked.Exchange(ref _hasPendingRecordingStart, 1);
    }

    private void RaisePendingRecordingStarted()
    {
        if (Interlocked.Exchange(ref _hasPendingRecordingStart, 0) == 1)
        {
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
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
