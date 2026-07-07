using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Capture.Image;

internal sealed class ImageCapturePostProcessor
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private readonly ImageCaptureFileNameGenerator _fileNameGenerator;

    public ImageCapturePostProcessor(
        IClipboardService clipboardService,
        IFileSystem fileSystem,
        ISettingsService settingsService,
        IStorageService storageService,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService,
        ImageCaptureFileNameGenerator fileNameGenerator)
    {
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _storageService = storageService;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
        _fileNameGenerator = fileNameGenerator;
    }

    public void Process(ImageFile imageFile)
    {
        AutoSaveImage(imageFile);
        AutoCopyImage(imageFile);
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
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoCopyImage", e);
            }
        });
    }

    private void AutoSaveImage(ImageFile imageFile)
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

                string newFilePath = Path.Combine(screenshotsFolder, _fileNameGenerator.GetNewCaptureFileName());

                _fileSystem.CopyFile(imageFile.FilePath, newFilePath, true);
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoSaveImage", e);
            }
        });
    }
}
