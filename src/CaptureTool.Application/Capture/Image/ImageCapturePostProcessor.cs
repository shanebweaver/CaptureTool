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

namespace CaptureTool.Application.Capture.Image;

internal sealed class ImageCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly CaptureFileAllocator _fileAllocator;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ILogService _logService;
    private readonly ImageCaptureFileNameGenerator _fileNameGenerator;
    private readonly ICaptureAssetLifecycleService _captureAssetLifecycleService;
    private readonly ITelemetryService? _telemetryService;

    public ImageCapturePostProcessor(
        IClipboardService clipboardService,
        CaptureFileAllocator fileAllocator,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ILogService logService,
        ImageCaptureFileNameGenerator fileNameGenerator,
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

    public void Process(ImageFile imageFile)
    {
        CaptureId? captureId = TryFinalizeCapture(imageFile.FilePath);
        AutoSaveImage(imageFile, captureId);
        AutoCopyImage(imageFile);
    }

    private CaptureId? TryFinalizeCapture(string retainedSourcePath)
    {
        try
        {
            return _captureAssetLifecycleService.TryFinalize(
                retainedSourcePath,
                CaptureFileType.Image);
        }
        catch (Exception exception)
        {
            _logService.LogException(
                exception,
                "Failed to record the finalized image Capture Asset.");
            return null;
        }
    }

    private void AutoCopyImage(ImageFile imageFile)
    {
        _taskEnvironment.TryExecute(async () =>
        {
            try
            {
                bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
                if (!autoCopy)
                {
                    return;
                }

                ClipboardFile clipboardFile = new(imageFile.FilePath);
                await _clipboardService.CopyBitmapAsync(clipboardFile);
                TrackOutput("auto_copy", TelemetryOutcomes.Succeeded);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Activity error: AutoCopyImage");
                TrackOutput("auto_copy", TelemetryOutcomes.Failed);
            }
        });
    }

    private void AutoSaveImage(ImageFile imageFile, CaptureId? captureId)
    {
        _taskEnvironment.TryExecute(() =>
        {
            try
            {
                bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);
                if (!autoSave)
                {
                    return;
                }

                string screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(screenshotsFolder))
                {
                    screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
                }

                string newFilePath = _fileAllocator.CopyToUniqueFile(
                    imageFile.FilePath,
                    screenshotsFolder,
                    _fileNameGenerator.GetNewCaptureFileName);
                imageFile.PersistentFilePath = newFilePath;
                _captureAssetLifecycleService.TrySetPreferredOpenPath(
                    captureId,
                    imageFile.FilePath,
                    newFilePath);
                TrackOutput("auto_save", TelemetryOutcomes.Succeeded);
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Activity error: AutoSaveImage");
                TrackOutput("auto_save", TelemetryOutcomes.Failed);
            }
        });
    }

    private void TrackOutput(string operation, string outcome)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "image",
                [TelemetryProperties.Outcome] = outcome,
                [TelemetryProperties.Source] = "capture_post_processor"
            });
    }
}
