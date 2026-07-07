using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Features.AudioCapture;

internal sealed class AudioCaptureWorkflow : IAudioCaptureWorkflow
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IStorageService _storageService;
    private readonly AudioCaptureStateStore _stateStore;
    private readonly AudioCaptureFileNameGenerator _fileNameGenerator;

    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<AudioFile>? NewAudioCaptured;

    public bool IsRecording => Snapshot.IsRecording;
    public bool IsPaused => Snapshot.IsPaused;
    public bool IsMuted => Snapshot.IsMuted;
    public bool IsDesktopAudioEnabled => Snapshot.IsDesktopAudioEnabled;
    public string? SelectedAudioInputSourceId => Snapshot.SelectedAudioInputSourceId;
    public AudioCaptureState CaptureState => Snapshot.CaptureState;

    private AudioCaptureStateSnapshot Snapshot => _stateStore.GetSnapshot();

    public AudioCaptureWorkflow(
        IAudioRecorder audioRecorder,
        IStorageService storageService,
        AudioCaptureStateStore stateStore,
        AudioCaptureFileNameGenerator fileNameGenerator)
    {
        _audioRecorder = audioRecorder;
        _storageService = storageService;
        _stateStore = stateStore;
        _fileNameGenerator = fileNameGenerator;
    }

    public void StartCapture()
    {
        string tempAudioPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName());

        AudioCaptureSession session = _stateStore.StartSession(tempAudioPath);

        try
        {
            _audioRecorder.StartCapture(session.TempAudioPath);
        }
        catch
        {
            _stateStore.StopSession(session.Id);
            throw;
        }

        CaptureStateChanged?.Invoke(this, AudioCaptureState.Recording);
    }

    public AudioFile StopCapture()
    {
        Guid sessionId = _stateStore.GetRequiredActiveSessionId();
        AudioFile audioFile;

        try
        {
            audioFile = _audioRecorder.StopCapture();
        }
        finally
        {
            _stateStore.StopSession(sessionId);
        }

        CaptureStateChanged?.Invoke(this, AudioCaptureState.Stopped);
        NewAudioCaptured?.Invoke(this, audioFile);

        return audioFile;
    }

    public void PauseCapture()
    {
        Guid sessionId = _stateStore.GetRequiredActiveSessionId();
        bool shouldResume = IsPaused;

        if (shouldResume)
        {
            _audioRecorder.Resume();
        }
        else
        {
            _audioRecorder.Pause();
        }

        AudioCaptureState newState = _stateStore.PauseOrResume(sessionId);
        CaptureStateChanged?.Invoke(this, newState);
    }

    public void ToggleLocalAudio()
    {
        _audioRecorder.ToggleDesktopAudio();

        AudioCaptureStateSnapshot snapshot = _stateStore.UpdateSettings(
            settings => settings.WithDesktopAudioEnabled(!settings.IsDesktopAudioEnabled));

        DesktopAudioStateChanged?.Invoke(this, snapshot.IsDesktopAudioEnabled);
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        AudioCaptureStateSnapshot snapshot = _stateStore.UpdateSettings(settings => settings.WithAudioInputSource(sourceId));
        _audioRecorder.SetAudioInputSource(snapshot.SelectedAudioInputSourceId);
    }

    public void ToggleMute()
    {
        _audioRecorder.ToggleMute();

        AudioCaptureStateSnapshot snapshot = _stateStore.UpdateSettings(settings => settings.WithMuted(!settings.IsMuted));
        MutedStateChanged?.Invoke(this, snapshot.IsMuted);
    }
}
