using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Features.VideoCapture;

internal sealed class VideoCaptureWorkflow : IVideoCaptureWorkflow
{
    private readonly IScreenRecorder _screenRecorder;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly VideoCaptureStateStore _stateStore;
    private readonly VideoCapturePostProcessor _postProcessor;
    private readonly VideoCaptureFileNameGenerator _fileNameGenerator;

    public event EventHandler<VideoFile>? NewVideoCaptured;
    public event EventHandler? RecordingStarted;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public bool IsDesktopAudioEnabled => Snapshot.IsDesktopAudioEnabled;
    public bool IsAudioInputMuted => Snapshot.IsAudioInputMuted;
    public int AudioInputVolumePercentage => Snapshot.AudioInputVolumePercentage;
    public bool IsRecording => Snapshot.IsRecording;
    public bool IsFinalizing => Snapshot.IsFinalizing;
    public bool IsPaused => Snapshot.IsPaused;
    public string? SelectedAudioInputSourceId => Snapshot.SelectedAudioInputSourceId;

    private VideoCaptureStateSnapshot Snapshot => _stateStore.GetSnapshot();

    public VideoCaptureWorkflow(
        IScreenRecorder screenRecorder,
        ISettingsService settingsService,
        IStorageService storageService,
        IBackgroundTaskRunner backgroundTaskRunner,
        VideoCaptureStateStore stateStore,
        VideoCapturePostProcessor postProcessor,
        VideoCaptureFileNameGenerator fileNameGenerator)
    {
        _screenRecorder = screenRecorder;
        _settingsService = settingsService;
        _storageService = storageService;
        _backgroundTaskRunner = backgroundTaskRunner;
        _stateStore = stateStore;
        _postProcessor = postProcessor;
        _fileNameGenerator = fileNameGenerator;

        _screenRecorder.RecordingStarted += OnRecordingStarted;
    }

    public void PrepareForVideoCapture()
    {
        bool defaultDesktopAudioEnabled = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        _stateStore.PrepareForVideoCapture(defaultDesktopAudioEnabled);
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        string tempVideoPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName());

        CaptureRecordingTarget target = CreateRecordingTarget(args);
        VideoCaptureSession session = _stateStore.StartSession(tempVideoPath, target);

        try
        {
            _screenRecorder.StartRecording(session.CreateRecordingOptions());
        }
        catch
        {
            _stateStore.ClearSession(session.Id);
            throw;
        }
    }

    public PendingVideoFile StopVideoCapture()
    {
        VideoCaptureFinalization finalization = _stateStore.BeginFinalizing();

        if (finalization.WasPaused)
        {
            PausedStateChanged?.Invoke(this, false);
        }

        _backgroundTaskRunner.Run(
            () => FinalizeVideo(finalization),
            "Failed to finalize video capture.");

        NewVideoCaptured?.Invoke(this, finalization.PendingVideo);
        return finalization.PendingVideo;
    }

    public void CancelVideoCapture()
    {
        VideoCaptureSession? session = _stateStore.GetCancelableSession();
        if (session is null)
        {
            return;
        }

        bool wasPaused = session.Status == VideoCaptureStatus.Paused;

        try
        {
            _screenRecorder.StopRecording();
        }
        finally
        {
            _stateStore.ClearSession(session.Id);

            if (wasPaused)
            {
                PausedStateChanged?.Invoke(this, false);
            }
        }
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        _stateStore.UpdateAudioSettings(settings => settings.WithDesktopAudioEnabled(value));
        DesktopAudioStateChanged?.Invoke(this, value);
    }

    public void ToggleDesktopAudioCapture(bool enabled)
    {
        VideoCaptureStateSnapshot snapshot = Snapshot;
        if (snapshot.IsRecording)
        {
            _screenRecorder.SetAudioCaptureEnabled(snapshot.AudioSettings.ShouldCaptureAudio);
        }
    }

    public void SetIsAudioInputMuted(bool value)
    {
        VideoCaptureStateSnapshot snapshot = _stateStore.UpdateAudioSettings(settings => settings.WithAudioInputMuted(value));

        if (snapshot.IsRecording)
        {
            _screenRecorder.SetAudioCaptureEnabled(snapshot.AudioSettings.ShouldCaptureAudio);
        }
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        VideoCaptureStateSnapshot snapshot = _stateStore.UpdateAudioSettings(settings => settings.WithAudioInputSource(sourceId));

        if (snapshot.IsRecording)
        {
            _screenRecorder.SetAudioInputSource(snapshot.AudioSettings.SelectedAudioInputSourceId);
            _screenRecorder.SetAudioCaptureEnabled(snapshot.AudioSettings.ShouldCaptureAudio);
        }
    }

    public void SetAudioInputVolume(int volumePercentage)
    {
        VideoCaptureStateSnapshot snapshot = _stateStore.UpdateAudioSettings(settings => settings.WithAudioInputVolume(volumePercentage));

        if (snapshot.IsRecording)
        {
            _screenRecorder.SetAudioInputVolume(snapshot.AudioSettings.AudioInputVolumePercentage);
        }
    }

    public void ToggleIsPaused(bool isPaused)
    {
        VideoCaptureSession session = _stateStore.GetRequiredActiveSession();
        bool isCurrentlyPaused = session.Status == VideoCaptureStatus.Paused;
        if (isPaused == isCurrentlyPaused)
        {
            return;
        }

        if (isPaused)
        {
            _screenRecorder.PauseRecording();
        }
        else
        {
            _screenRecorder.ResumeRecording();
        }

        if (_stateStore.SetPaused(session.Id, isPaused))
        {
            PausedStateChanged?.Invoke(this, isPaused);
        }
    }

    private void FinalizeVideo(VideoCaptureFinalization finalization)
    {
        try
        {
            _screenRecorder.StopRecording();

            finalization.PendingVideo.Complete();
            _postProcessor.Process(finalization.PendingVideo);
        }
        catch (Exception ex)
        {
            finalization.PendingVideo.Fail(ex);
            throw;
        }
        finally
        {
            _stateStore.ClearSession(finalization.SessionId);
        }
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        if (_stateStore.TryMarkRecordingStarted())
        {
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    private static CaptureRecordingTarget CreateRecordingTarget(NewCaptureArgs args)
    {
        return args.CaptureType switch
        {
            CaptureType.Window when args.WindowHandle != 0 => CaptureRecordingTarget.Window(args.WindowHandle),
            CaptureType.Rectangle or CaptureType.Window => CaptureRecordingTarget.Rectangle(
                args.Monitor.HMonitor,
                (int)Math.Round(args.Area.Left * args.Monitor.Scale),
                (int)Math.Round(args.Area.Top * args.Monitor.Scale),
                Math.Max(1, (int)Math.Round(args.Area.Width * args.Monitor.Scale)),
                Math.Max(1, (int)Math.Round(args.Area.Height * args.Monitor.Scale))),
            _ => CaptureRecordingTarget.Monitor(args.Monitor.HMonitor)
        };
    }
}
