using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ILogService _logService;
    private readonly VideoCaptureFileNameGenerator _fileNameGenerator;
    private readonly ICaptureAssetLifecycleService _captureAssetLifecycleService;
    private readonly ITelemetryService? _telemetryService;

    public VideoCapturePostProcessor(
        IClipboardService clipboardService,
        CaptureFileAllocator fileAllocator,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ILogService logService,
        VideoCaptureFileNameGenerator fileNameGenerator,
        ICaptureAssetLifecycleService captureAssetLifecycleService,
        ITelemetryService? telemetryService = null)
    {
        _clipboardService = clipboardService;
        _fileAllocator = fileAllocator;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _logService = logService;
        _fileNameGenerator = fileNameGenerator;
        _captureAssetLifecycleService = captureAssetLifecycleService;
        _telemetryService = telemetryService;
    }

    public void Process(VideoFile videoFile)
    {
        CaptureId? captureId = TryFinalizeCapture(videoFile.FilePath);
        AutoSaveVideo(videoFile, captureId);
        AutoCopyVideo(videoFile);
    }

    private CaptureId? TryFinalizeCapture(string retainedSourcePath)
    {
        try
        {
            return _captureAssetLifecycleService.TryFinalize(
                retainedSourcePath,
                CaptureFileType.Video);
        }
        catch (Exception exception)
        {
            _logService.LogException(
                exception,
                "Failed to record the finalized video Capture Asset.");
            return null;
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
                TrackOutput("auto_copy", TelemetryOutcomes.Succeeded);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Activity error: AutoCopyVideo");
                TrackOutput("auto_copy", TelemetryOutcomes.Failed);
            }
        });
    }

    private void AutoSaveVideo(VideoFile videoFile, CaptureId? captureId)
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

            string newFilePath = _fileAllocator.CopyToUniqueFile(
                videoFile.FilePath,
                videosFolder,
                _fileNameGenerator.GetNewCaptureFileName);
            _captureAssetLifecycleService.TrySetPreferredOpenPath(
                captureId,
                videoFile.FilePath,
                newFilePath);
            TrackOutput("auto_save", TelemetryOutcomes.Succeeded);
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Activity error: AutoSaveVideo");
            TrackOutput("auto_save", TelemetryOutcomes.Failed);
        }
    }

    private void TrackOutput(string operation, string outcome)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.Outcome] = outcome,
                [TelemetryProperties.Source] = "capture_post_processor"
            });
    }
}
