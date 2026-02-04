using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.TaskEnvironment;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.Capture;

public partial class CaptureToolVideoCaptureHandler : IVideoCaptureHandler
{
    private readonly IClipboardService _clipboardService;
    private readonly IScreenRecorder _screenRecorder;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ILogService _logService;
    private readonly IFeatureManager _featureManager;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private readonly IMetadataScannerRegistry? _metadataScannerRegistry;
    private readonly IRealTimeMetadataScanJobFactory? _scanJobFactory;

    private string? _tempVideoPath;
    private AudioSampleCallback? _audioSampleCallback;
    private VideoFrameCallback? _videoFrameCallback;
    private IRealTimeMetadataScanJob? _currentScanJob;

    public bool IsDesktopAudioEnabled { get; private set; }
    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }

    public event EventHandler<IVideoFile>? NewVideoCaptured;
    public event EventHandler<bool>? DesktopAudioStateChanged;
    public event EventHandler<bool>? PausedStateChanged;

    public CaptureToolVideoCaptureHandler(
        IClipboardService clipboardService,
        IScreenRecorder screenRecorder,
        ISettingsService settingsService,
        IStorageService storageService,
        ILogService logService,
        IFeatureManager featureManager,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService,
        IMetadataScannerRegistry? metadataScannerRegistry = null,
        IRealTimeMetadataScanJobFactory? scanJobFactory = null)
    {
        _clipboardService = clipboardService;
        _screenRecorder = screenRecorder;
        _settingsService = settingsService;
        _storageService = storageService;
        _logService = logService;
        _featureManager = featureManager;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
        _metadataScannerRegistry = metadataScannerRegistry;
        _scanJobFactory = scanJobFactory;

        IsDesktopAudioEnabled = true;
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("A video is already being recorded.");
        }

        IsRecording = true;

        DateTime timestamp = DateTime.Now;
        string fileName = $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.mp4";
        _tempVideoPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            fileName
        );

        // Start metadata collection if feature is enabled and factory/registry are available
        if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection) &&
            _scanJobFactory != null &&
            _metadataScannerRegistry != null)
        {
            _currentScanJob = _scanJobFactory.CreateJob(Guid.NewGuid(), _tempVideoPath, _metadataScannerRegistry);

            // Set callbacks - ScreenRecorderImpl will apply them to the session
            _audioSampleCallback = OnAudioSampleCallback;
            _screenRecorder.SetAudioSampleCallback(_audioSampleCallback);

            _videoFrameCallback = OnVideoFrameCallback;
            _screenRecorder.SetVideoFrameCallback(_videoFrameCallback);
        }

        // Start recording - callbacks will be automatically applied to the new session
        _screenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath, IsDesktopAudioEnabled);
    }

    private void OnVideoFrameCallback(ref VideoFrameData frameData)
    {
        // Process frame with metadata scanners
        _currentScanJob?.ProcessVideoFrame(ref frameData);
    }

    private void OnAudioSampleCallback(ref AudioSampleData sampleData)
    {
        // Process sample with metadata scanners
        _currentScanJob?.ProcessAudioSample(ref sampleData);
    }

    public PendingVideoFile StopVideoCapture()
    {
        if (!IsRecording || string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        IsRecording = false;
        IsPaused = false;
        string filePath = _tempVideoPath;
        _tempVideoPath = null;

        var pendingVideo = new PendingVideoFile(filePath);
        var currentScanJob = _currentScanJob;
        _currentScanJob = null;

        // Finalize video on a background thread to avoid blocking the UI
        Task.Run(() => FinalizeVideoAsync(pendingVideo, currentScanJob));

        NewVideoCaptured?.Invoke(this, pendingVideo);
        return pendingVideo;
    }

    private async Task FinalizeVideoAsync(PendingVideoFile pendingVideo, IRealTimeMetadataScanJob? currentScanJob)
    {
        try
        {
            _screenRecorder.SetAudioSampleCallback(null);
            _screenRecorder.SetVideoFrameCallback(null);
            _screenRecorder.StopRecording();

            pendingVideo.Complete();

            // Save metadata if collection was active
            if (currentScanJob != null)
            {
                try
                {
                    await currentScanJob.FinalizeAndSaveAsync();
                    _logService.LogInformation($"Saved metadata for video: {pendingVideo.FileName}");
                }
                catch (Exception ex)
                {
                    _logService.LogException(ex, $"Failed to save metadata for video: {pendingVideo.FileName}");
                }
            }

            AutoSaveVideo(pendingVideo, currentScanJob?.MetadataFilePath);
            AutoCopyVideo(pendingVideo);
        }
        catch (Exception ex)
        {
            pendingVideo.Fail(ex);
            throw;
        }
        finally
        {
            _audioSampleCallback = null;
            _videoFrameCallback = null;
        }
    }

    public void CancelVideoCapture()
    {
        try
        {
            if (!IsRecording)
            {
                return;
            }

            _screenRecorder.SetAudioSampleCallback(null);
            _screenRecorder.SetVideoFrameCallback(null);
            _screenRecorder.StopRecording();
        }
        finally
        {
            _audioSampleCallback = null;
            _videoFrameCallback = null;
            _tempVideoPath = null;
            _currentScanJob = null;
            IsRecording = false;
            IsPaused = false;
        }
    }

    public void SetIsDesktopAudioEnabled(bool value)
    {
        IsDesktopAudioEnabled = value;
        DesktopAudioStateChanged?.Invoke(this, value);
    }

    public void ToggleDesktopAudioCapture(bool enabled)
    {
        if (IsRecording)
        {
            _screenRecorder.ToggleAudioCapture(enabled);
        }
    }

    public void ToggleIsPaused(bool isPaused)
    {
        IsPaused = isPaused;
        PausedStateChanged?.Invoke(this, isPaused);

        if (IsRecording)
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
                _telemetryService.ActivityError("AutoCopyVideoFailed", e);
            }
        });
    }

    private void AutoSaveVideo(VideoFile videoFile, string? metadataFilePath = null)
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
            string fileName = Path.GetFileName(tempFilePath);
            string newFilePath = Path.Combine(videosFolder, $"capture_{Guid.NewGuid()}.mp4");

            File.Copy(tempFilePath, newFilePath, true);

            // Copy metadata file if it exists and the setting is enabled
            if (!string.IsNullOrWhiteSpace(metadataFilePath) &&
                File.Exists(metadataFilePath) &&
                _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection))
            {
                bool autoSaveMetadata = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave);
                if (autoSaveMetadata)
                {
                    string newMetadataFilePath = Path.ChangeExtension(newFilePath, MetadataFile.FileExtension);
                    File.Copy(metadataFilePath, newMetadataFilePath, true);
                }
            }
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError("AutoSaveVideoFailed", e);
        }
    }
}
