using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCaptureWorkflow : IAudioCaptureWorkflow
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly AudioCaptureStateStore _stateStore;
    private readonly AudioCapturePostProcessor _postProcessor;
    private readonly AudioCaptureFileNameGenerator _fileNameGenerator;
    private readonly ITelemetryService? _telemetryService;

    public event EventHandler<AudioCaptureState>? CaptureStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<AudioFile>? NewAudioCaptured;
    public event EventHandler<AudioCaptureLevel>? AudioLevelCaptured;

    public bool IsRecording => Snapshot.IsRecording;
    public bool IsPaused => Snapshot.IsPaused;
    public bool IsMuted => Snapshot.IsMuted;
    public bool IsDesktopAudioEnabled => Snapshot.IsDesktopAudioEnabled;
    public string? SelectedAudioInputSourceId => Snapshot.SelectedAudioInputSourceId;
    public AudioCaptureState CaptureState => Snapshot.CaptureState;

    private AudioCaptureStateSnapshot Snapshot => _stateStore.GetSnapshot();

    public AudioCaptureWorkflow(
        IAudioRecorder audioRecorder,
        IFileSystem fileSystem,
        ISettingsService settingsService,
        IStorageService storageService,
        CaptureFileAllocator fileAllocator,
        AudioCaptureStateStore stateStore,
        AudioCapturePostProcessor postProcessor,
        AudioCaptureFileNameGenerator fileNameGenerator,
        ITelemetryService? telemetryService = null)
    {
        _audioRecorder = audioRecorder;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _storageService = storageService;
        _fileAllocator = fileAllocator;
        _stateStore = stateStore;
        _postProcessor = postProcessor;
        _fileNameGenerator = fileNameGenerator;
        _telemetryService = telemetryService;

        _audioRecorder.AudioLevelCaptured += OnAudioLevelCaptured;
    }

    public void StartCapture()
    {
        bool defaultDesktopAudioEnabled = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled);
        _stateStore.PrepareForAudioCapture(defaultDesktopAudioEnabled);
        TrackCapture(TelemetryEvents.CaptureRequested);

        string tempAudioPath = _fileAllocator.ReserveUniqueFile(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName);

        AudioCaptureSession? session = null;

        try
        {
            session = _stateStore.StartSession(tempAudioPath);
            _audioRecorder.StartCapture(session.TempAudioPath);
        }
        catch
        {
            if (session is not null)
            {
                _stateStore.StopSession(session.Id);
            }

            _fileAllocator.TryDeleteFile(tempAudioPath);
            TrackCapture(TelemetryEvents.CaptureFailed, TelemetryOutcomes.Failed);
            throw;
        }

        CaptureStateChanged?.Invoke(this, AudioCaptureState.Recording);
        TrackCapture(TelemetryEvents.CaptureStarted);
    }

    public AudioFile StopCapture()
    {
        Guid sessionId = _stateStore.GetRequiredActiveSessionId();
        AudioFile audioFile;

        try
        {
            audioFile = _audioRecorder.StopCapture();
        }
        catch
        {
            TrackCapture(TelemetryEvents.CaptureFailed, TelemetryOutcomes.Failed);
            throw;
        }
        finally
        {
            _stateStore.StopSession(sessionId);
        }

        CaptureStateChanged?.Invoke(this, AudioCaptureState.Stopped);
        NewAudioCaptured?.Invoke(this, audioFile);
        _postProcessor.Process(audioFile);
        TrackCapture(TelemetryEvents.CaptureCompleted, TelemetryOutcomes.Succeeded);
        return audioFile;
    }

    public void CancelCapture()
    {
        AudioCaptureSession? session = _stateStore.GetCancelableSession();
        if (session is null)
        {
            return;
        }

        AudioFile? audioFile;
        try
        {
            audioFile = _audioRecorder.StopCapture();
        }
        catch
        {
            TrackCapture(TelemetryEvents.CaptureFailed, TelemetryOutcomes.Failed);
            throw;
        }
        finally
        {
            _stateStore.StopSession(session.Id);
            CaptureStateChanged?.Invoke(this, AudioCaptureState.Stopped);
        }

        try
        {
            DeleteCanceledAudioFile(session.TempAudioPath);
            if (audioFile is not null &&
                !string.Equals(audioFile.FilePath, session.TempAudioPath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteCanceledAudioFile(audioFile.FilePath);
            }

            TrackCapture(TelemetryEvents.CaptureCanceled, TelemetryOutcomes.Canceled);
        }
        catch
        {
            TrackCapture(TelemetryEvents.CaptureFailed, TelemetryOutcomes.Failed);
            throw;
        }
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

    private void OnAudioLevelCaptured(object? sender, AudioCaptureLevel level)
    {
        if (IsRecording && !IsPaused)
        {
            AudioLevelCaptured?.Invoke(this, level);
        }
    }

    private void DeleteCanceledAudioFile(string filePath)
    {
        if (_fileSystem.FileExists(filePath))
        {
            _fileSystem.DeleteFile(filePath);
        }
    }

    private void TrackCapture(string eventName, string? outcome = null)
    {
        AudioCaptureStateSnapshot snapshot = Snapshot;
        var properties = new Dictionary<string, object?>
        {
            [TelemetryProperties.MediaType] = "audio",
            [TelemetryProperties.CaptureType] = "audio_only",
            [TelemetryProperties.DesktopAudioEnabled] = snapshot.IsDesktopAudioEnabled,
            [TelemetryProperties.AudioInputEnabled] =
                snapshot.SelectedAudioInputSourceId is not null && !snapshot.IsMuted
        };

        if (outcome is not null)
        {
            properties[TelemetryProperties.Outcome] = outcome;
        }

        _telemetryService?.TrackEvent(eventName, properties);
    }
}
