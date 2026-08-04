using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Runtime.InteropServices;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCaptureWorkflow : IVideoCaptureWorkflow
{
    private readonly IScreenRecorder _screenRecorder;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IVideoCaptureSupportService _videoCaptureSupportService;
    private readonly VideoCaptureStateStore _stateStore;
    private readonly VideoCapturePostProcessor _postProcessor;
    private readonly VideoCaptureFileNameGenerator _fileNameGenerator;
    private readonly ITelemetryService? _telemetryService;
    private readonly Lock _audioRoutingLock = new();

    public event EventHandler<VideoFile>? NewVideoCaptured;
    public event EventHandler? RecordingStarted;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? AudioInputMutedStateChanged;
    public event EventHandler<string?>? AudioInputSourceChanged;
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
        CaptureFileAllocator fileAllocator,
        IBackgroundTaskRunner backgroundTaskRunner,
        IVideoCaptureSupportService videoCaptureSupportService,
        VideoCaptureStateStore stateStore,
        VideoCapturePostProcessor postProcessor,
        VideoCaptureFileNameGenerator fileNameGenerator,
        ITelemetryService? telemetryService = null)
    {
        _screenRecorder = screenRecorder;
        _settingsService = settingsService;
        _storageService = storageService;
        _fileAllocator = fileAllocator;
        _backgroundTaskRunner = backgroundTaskRunner;
        _videoCaptureSupportService = videoCaptureSupportService;
        _stateStore = stateStore;
        _postProcessor = postProcessor;
        _fileNameGenerator = fileNameGenerator;
        _telemetryService = telemetryService;

        _screenRecorder.RecordingStarted += OnRecordingStarted;
    }

    public void PrepareForVideoCapture()
    {
        bool defaultDesktopAudioEnabled = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        lock (_audioRoutingLock)
        {
            _stateStore.PrepareForVideoCapture(defaultDesktopAudioEnabled);
        }
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        TrackCapture(TelemetryEvents.CaptureRequested, args.CaptureType.ToString(), Snapshot.AudioSettings);
        VideoCaptureSupportStatus supportStatus;
        try
        {
            supportStatus = _videoCaptureSupportService.GetSupportStatus();
        }
        catch (Exception)
        {
            supportStatus = VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.GraphicsCapture);
        }

        if (!supportStatus.IsSupported)
        {
            TrackCapture(
                TelemetryEvents.CaptureFailed,
                args.CaptureType.ToString(),
                Snapshot.AudioSettings,
                TelemetryOutcomes.Failed,
                TelemetryFailureStages.RecorderStart,
                ClassifyUnsupportedReason(supportStatus.UnsupportedReason));
            throw new VideoCaptureNotSupportedException(supportStatus.UnsupportedReason);
        }

        string tempVideoPath = _fileAllocator.ReserveUniqueFile(
            _storageService.GetApplicationTemporaryFolderPath(),
            _fileNameGenerator.GetNewCaptureFileName);

        CaptureRecordingTarget target = CreateRecordingTarget(args);
        VideoCaptureSession? session = null;

        try
        {
            lock (_audioRoutingLock)
            {
                session = _stateStore.StartSession(tempVideoPath, target, args.CaptureType);
                _screenRecorder.StartRecording(session.CreateRecordingOptions());
            }
        }
        catch (Exception ex)
        {
            if (session is not null)
            {
                _stateStore.ClearSession(session.Id);
            }

            _fileAllocator.TryDeleteFile(tempVideoPath);
            TrackCapture(
                TelemetryEvents.CaptureFailed,
                args.CaptureType.ToString(),
                session?.AudioSettings ?? Snapshot.AudioSettings,
                TelemetryOutcomes.Failed,
                TelemetryFailureStages.RecorderStart,
                ClassifyRecorderStartFailure(ex));
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

    public void CancelVideoCapture(CancelVideoCaptureReason reason = CancelVideoCaptureReason.User)
    {
        VideoCaptureSession? session = _stateStore.GetCancelableSession();
        if (session is null)
        {
            return;
        }

        bool wasPaused = session.Status == VideoCaptureStatus.Paused;
        bool isStartTimeout = reason == CancelVideoCaptureReason.StartTimeout;

        try
        {
            _screenRecorder.StopRecording();
        }
        catch
        {
            TrackCapture(
                TelemetryEvents.CaptureFailed,
                session.CaptureType.ToString(),
                session.AudioSettings,
                TelemetryOutcomes.Failed,
                isStartTimeout ? TelemetryFailureStages.FirstFrame : null,
                isStartTimeout ? TelemetryFailureReasons.StartTimeout : null);
            throw;
        }
        finally
        {
            _stateStore.ClearSession(session.Id);

            if (wasPaused)
            {
                PausedStateChanged?.Invoke(this, false);
            }
        }

        TrackCapture(
            isStartTimeout ? TelemetryEvents.CaptureFailed : TelemetryEvents.CaptureCanceled,
            session.CaptureType.ToString(),
            session.AudioSettings,
            isStartTimeout ? TelemetryOutcomes.Failed : TelemetryOutcomes.Canceled,
            isStartTimeout ? TelemetryFailureStages.FirstFrame : null,
            isStartTimeout ? TelemetryFailureReasons.StartTimeout : null);
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        (VideoCaptureStateSnapshot snapshot, bool changed) = UpdateAudioSettings(
            settings => settings.WithDesktopAudioEnabled(value),
            settings => _screenRecorder.SetAudioCaptureEnabled(settings.ShouldCaptureDesktopAudio));

        if (changed)
        {
            DesktopAudioStateChanged?.Invoke(this, snapshot.IsDesktopAudioEnabled);
        }
    }

    public void SetIsAudioInputMuted(bool value)
    {
        (VideoCaptureStateSnapshot snapshot, bool changed) = UpdateAudioSettings(
            settings => settings.WithAudioInputMuted(value),
            settings => _screenRecorder.SetAudioInputSource(settings.ActiveAudioInputSourceId));

        if (changed)
        {
            AudioInputMutedStateChanged?.Invoke(this, snapshot.IsAudioInputMuted);
        }
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        (VideoCaptureStateSnapshot snapshot, bool changed) = UpdateAudioSettings(
            settings => settings.WithAudioInputSource(sourceId),
            settings => _screenRecorder.SetAudioInputSource(settings.ActiveAudioInputSourceId));

        if (changed)
        {
            AudioInputSourceChanged?.Invoke(this, snapshot.SelectedAudioInputSourceId);
        }
    }

    public void SetAudioInputVolume(int volumePercentage)
    {
        UpdateAudioSettings(
            settings => settings.WithAudioInputVolume(volumePercentage),
            settings => _screenRecorder.SetAudioInputVolume(settings.AudioInputVolumePercentage));
    }

    private (VideoCaptureStateSnapshot Snapshot, bool Changed) UpdateAudioSettings(
        Func<VideoCaptureAudioSettings, VideoCaptureAudioSettings> update,
        Action<VideoCaptureAudioSettings> applyToRecorder)
    {
        lock (_audioRoutingLock)
        {
            VideoCaptureStateSnapshot current = Snapshot;
            VideoCaptureAudioSettings candidate = update(current.AudioSettings);
            if (candidate == current.AudioSettings)
            {
                return (current, false);
            }

            if (current.IsRecording)
            {
                applyToRecorder(candidate);
            }

            return (_stateStore.SetAudioSettings(candidate), true);
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
            TrackCapture(
                TelemetryEvents.CaptureCompleted,
                finalization.CaptureType.ToString(),
                finalization.AudioSettings,
                TelemetryOutcomes.Succeeded);
        }
        catch (Exception ex)
        {
            finalization.PendingVideo.Fail(ex);
            TrackCapture(
                TelemetryEvents.CaptureFailed,
                finalization.CaptureType.ToString(),
                finalization.AudioSettings,
                TelemetryOutcomes.Failed);
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
            VideoCaptureSession session = _stateStore.GetRequiredActiveSession();
            TrackCapture(
                TelemetryEvents.CaptureStarted,
                session.CaptureType.ToString(),
                session.AudioSettings);
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TrackCapture(
        string eventName,
        string captureType,
        VideoCaptureAudioSettings audioSettings,
        string? outcome = null,
        string? failureStage = null,
        string? failureReason = null)
    {
        var properties = new Dictionary<string, object?>
        {
            [TelemetryProperties.MediaType] = "video",
            [TelemetryProperties.CaptureType] = captureType,
            [TelemetryProperties.DesktopAudioEnabled] = audioSettings.ShouldCaptureDesktopAudio,
            [TelemetryProperties.AudioInputEnabled] =
                audioSettings.ActiveAudioInputSourceId is not null
        };

        if (outcome is not null)
        {
            properties[TelemetryProperties.Outcome] = outcome;
        }

        if (failureStage is not null)
        {
            properties[TelemetryProperties.FailureStage] = failureStage;
        }

        if (failureReason is not null)
        {
            properties[TelemetryProperties.FailureReason] = failureReason;
        }

        _telemetryService?.TrackEvent(eventName, properties);
    }

    private static string ClassifyRecorderStartFailure(Exception exception)
    {
        foreach (Exception candidate in EnumerateExceptions(exception))
        {
            string? reason = candidate switch
            {
                VideoCaptureTargetUnavailableException => TelemetryFailureReasons.TargetUnavailable,
                VideoCaptureNotSupportedException notSupportedException =>
                    ClassifyUnsupportedReason(notSupportedException.Reason),
                PlatformNotSupportedException => TelemetryFailureReasons.PlatformUnsupported,
                DllNotFoundException or EntryPointNotFoundException or TypeLoadException or FileNotFoundException =>
                    TelemetryFailureReasons.ComponentUnavailable,
                UnauthorizedAccessException => TelemetryFailureReasons.AccessDenied,
                OutOfMemoryException => TelemetryFailureReasons.ResourceExhausted,
                ArgumentException => TelemetryFailureReasons.InvalidConfiguration,
                NotSupportedException => TelemetryFailureReasons.ConfigurationUnsupported,
                IOException => TelemetryFailureReasons.OutputUnavailable,
                COMException comException => ClassifyComFailure(comException.HResult),
                ExternalException externalException => ClassifyComFailure(externalException.ErrorCode),
                _ => null
            };

            if (reason is not null)
            {
                return reason;
            }
        }

        return TelemetryFailureReasons.InitializationFailed;
    }

    private static string ClassifyUnsupportedReason(VideoCaptureUnsupportedReason reason)
    {
        return reason switch
        {
            VideoCaptureUnsupportedReason.OperatingSystem => TelemetryFailureReasons.PlatformUnsupported,
            VideoCaptureUnsupportedReason.GraphicsCapture => TelemetryFailureReasons.GraphicsUnsupported,
            _ => TelemetryFailureReasons.InitializationFailed
        };
    }

    private static string? ClassifyComFailure(int hResult)
    {
        return hResult switch
        {
            unchecked((int)0x80070005) => TelemetryFailureReasons.AccessDenied,
            unchecked((int)0x80040154) or unchecked((int)0x80004002) =>
                TelemetryFailureReasons.ComponentUnavailable,
            unchecked((int)0xC00D36B0) or unchecked((int)0xC00D5212) =>
                TelemetryFailureReasons.ComponentUnavailable,
            unchecked((int)0x887A0004) or unchecked((int)0x887A0022) =>
                TelemetryFailureReasons.GraphicsUnsupported,
            unchecked((int)0x80070057) => TelemetryFailureReasons.InvalidConfiguration,
            unchecked((int)0x80004001) or unchecked((int)0x80070032) or
                unchecked((int)0xC00D36B4) or unchecked((int)0xC00D36C4) =>
                TelemetryFailureReasons.ConfigurationUnsupported,
            unchecked((int)0x8007000E) => TelemetryFailureReasons.ResourceExhausted,
            _ => null
        };
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);

        while (pending.TryPop(out Exception? current))
        {
            yield return current;

            if (current is AggregateException aggregateException)
            {
                for (int index = aggregateException.InnerExceptions.Count - 1; index >= 0; index--)
                {
                    pending.Push(aggregateException.InnerExceptions[index]);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
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
