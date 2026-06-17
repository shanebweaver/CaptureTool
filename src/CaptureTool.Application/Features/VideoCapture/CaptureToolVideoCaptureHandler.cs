using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
namespace CaptureTool.Application.Features.VideoCapture;

public partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    internal enum CaptureState
    {
        Idle,
        Recording,
        Finalizing
    }

    private readonly IClipboardService _clipboardService;
    private readonly IScreenRecorder _screenRecorder;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;

    private string? _tempVideoPath;

    private CaptureState _captureState = CaptureState.Idle;

    public bool IsDesktopAudioEnabled { get; private set; }
    public bool IsAudioInputMuted { get; private set; }
    public int AudioInputVolumePercentage { get; private set; } = 100;
    public bool IsRecording => _captureState == CaptureState.Recording;
    public bool IsFinalizing => _captureState == CaptureState.Finalizing;
    public bool IsPaused { get; private set; }
    public string? SelectedAudioInputSourceId { get; private set; }

    public event EventHandler<IVideoFile>? NewVideoCaptured;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public CaptureToolVideoCaptureHandler(
        IClipboardService clipboardService,
        IScreenRecorder screenRecorder,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService)
    {
        _clipboardService = clipboardService;
        _screenRecorder = screenRecorder;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
    }

    public void PrepareForVideoCapture()
    {
        IsDesktopAudioEnabled = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        IsAudioInputMuted = false;
        AudioInputVolumePercentage = 100;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        if (_captureState != CaptureState.Idle)
        {
            throw new InvalidOperationException("A video is already being recorded.");
        }

        _tempVideoPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            GetNewCaptureFileName()
        );

        try
        {
            CaptureRecordingTarget target = CreateRecordingTarget(args);
            var startResult = _screenRecorder.StartRecording(new CaptureRecordingOptions(
                target,
                _tempVideoPath,
                ShouldCaptureAudio(),
                AudioInputSourceId: SelectedAudioInputSourceId,
                AudioInputVolumePercentage: AudioInputVolumePercentage));

            startResult.EnsureSuccess();
            UpdateCaptureState(CaptureState.Recording);
        }
        catch
        {
            _tempVideoPath = null;
            UpdateCaptureState(CaptureState.Idle);
            throw;
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

    public PendingVideoFile StopVideoCapture()
    {
        if (_captureState != CaptureState.Recording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        UpdateCaptureState(CaptureState.Finalizing);

        var pendingVideo = new PendingVideoFile(_tempVideoPath);

        // Finalize video on a background thread to avoid blocking the UI
        Task.Run(() => FinalizeVideo(pendingVideo));

        NewVideoCaptured?.Invoke(this, pendingVideo);
        return pendingVideo;
    }

    private void FinalizeVideo(PendingVideoFile pendingVideo)
    {
        try
        {
            _screenRecorder.StopRecording().EnsureSuccess();

            pendingVideo.Complete();

            AutoSaveVideo(pendingVideo);
            AutoCopyVideo(pendingVideo);
        }
        catch (Exception ex)
        {
            pendingVideo.Fail(ex);
            throw;
        }
        finally
        {
            _tempVideoPath = null;
            IsPaused = false;

            UpdateCaptureState(CaptureState.Idle);
        }
    }

    public void CancelVideoCapture()
    {
        try
        {
            if (_captureState != CaptureState.Recording)
            {
                return;
            }

            _screenRecorder.RegisterAudioSampleCallback(null);
            _screenRecorder.RegisterVideoFrameCallback(null);
            _screenRecorder.StopRecording();
        }
        finally
        {
            _tempVideoPath = null;
            IsPaused = false;
            UpdateCaptureState(CaptureState.Idle);
        }
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        IsDesktopAudioEnabled = value;
        DesktopAudioStateChanged?.Invoke(this, value);
    }

    public void ToggleDesktopAudioCapture(bool enabled)
    {
        if (_captureState == CaptureState.Recording)
        {
            _screenRecorder.SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    public void SetIsAudioInputMuted(bool value)
    {
        IsAudioInputMuted = value;

        if (_captureState == CaptureState.Recording)
        {
            _screenRecorder.SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    public void SelectAudioInputSource(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            SelectedAudioInputSourceId = null;
            if (_captureState == CaptureState.Recording)
            {
                _screenRecorder.SetAudioInputSource(null);
                _screenRecorder.SetAudioCaptureEnabled(ShouldCaptureAudio());
            }

            return;
        }

        SelectedAudioInputSourceId = sourceId;

        if (_captureState == CaptureState.Recording)
        {
            _screenRecorder.SetAudioInputSource(sourceId);
            _screenRecorder.SetAudioCaptureEnabled(ShouldCaptureAudio());
        }
    }

    public void SetAudioInputVolume(int volumePercentage)
    {
        AudioInputVolumePercentage = Math.Clamp(volumePercentage, 0, 100);

        if (_captureState == CaptureState.Recording)
        {
            _screenRecorder.SetAudioInputVolume(AudioInputVolumePercentage);
        }
    }

    private bool ShouldCaptureAudio()
        => !IsAudioInputMuted && (IsDesktopAudioEnabled || !string.IsNullOrWhiteSpace(SelectedAudioInputSourceId));

    public void ToggleIsPaused(bool isPaused)
    {
        IsPaused = isPaused;
        PausedStateChanged?.Invoke(this, isPaused);

        if (_captureState == CaptureState.Recording)
        {
            if (isPaused)
            {
                _screenRecorder.PauseRecording();
            }
            else
            {
                _screenRecorder.ResumeRecording();
            }
        }
    }

    private void AutoCopyVideo(VideoFile videoFile)
    {
        _taskEnvironment.TryExecute(async () =>
        {
            try
            {
                bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
                if (!autoCopy)
                {
                    return;
                }

                ClipboardFile clipboardFile = new(videoFile.FilePath);
                await _clipboardService.CopyFileAsync(clipboardFile);
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoCopyVideo", e);
            }
        });
    }

    private void AutoSaveVideo(VideoFile videoFile)
    {
        try
        {
            bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
            if (!autoSave)
            {
                return;
            }

            string videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(videosFolder))
            {
                videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
            }

            string tempFilePath = videoFile.FilePath;
            string newFilePath = Path.Combine(videosFolder, GetNewCaptureFileName());

            File.Copy(tempFilePath, newFilePath, true);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError("AutoSaveVideo", e);
        }
    }

    private static string GetNewCaptureFileName()
    {
        DateTime timestamp = DateTime.Now;
        return $"Capture_{timestamp:yyyy-MM-dd}_{timestamp:FFFFF}.mp4";
    }

    internal void UpdateCaptureState(CaptureState newState)
    {
        _captureState = newState;
    }
}
