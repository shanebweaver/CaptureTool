using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Features.ImageCapture;

internal partial class CaptureToolImageCaptureHandler : IImageCaptureHandler
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly IScreenCapture _screenCapture;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private readonly IClock _clock;

    public event EventHandler<ImageFile>? NewImageCaptured;

    public CaptureToolImageCaptureHandler(
        IClipboardService clipboardService,
        IFileSystem fileSystem,
        IStorageService storageService,
        ISettingsService settingsService,
        IScreenCapture screenCapture,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService,
        IClock clock)
    {
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
        _storageService = storageService;
        _settingsService = settingsService;
        _screenCapture = screenCapture;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
        _clock = clock;
    }

    public ImageFile PerformAllScreensCapture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        return PerformMultiMonitorImageCapture(monitors);
    }

    public ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors)
    {
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            GetNewCaptureFileName()
        );

        Image combined = _screenCapture.CombineMonitors(monitors);
        _screenCapture.SaveImageToFile(combined, tempPath);

        ImageFile imageFile = new(tempPath);
        AutoSaveImage(imageFile);
        AutoCopyImage(imageFile);

        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
    }

    public ImageFile PerformImageCapture(NewCaptureArgs args)
    {
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            GetNewCaptureFileName()
        );

        MonitorCaptureResult monitor = args.Monitor;
        Rectangle area = args.Area;
        using Bitmap image = _screenCapture.CreateBitmapFromMonitorCaptureResult(monitor);
        using Bitmap cropped = _screenCapture.CreateCroppedBitmap(image, area, monitor.Scale);
        _screenCapture.SaveImageToFile(cropped, tempPath);

        ImageFile imageFile = new(tempPath);
        AutoSaveImage(imageFile);
        AutoCopyImage(imageFile);

        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
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

                string tempFilePath = imageFile.FilePath;
                string newFilePath = Path.Combine(screenshotsFolder, GetNewCaptureFileName());

                _fileSystem.CopyFile(tempFilePath, newFilePath, true);
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoSaveImage", e);
            }
        });
    }

    private string GetNewCaptureFileName()
    {
        DateTime timestamp = _clock.Now;
        return $"Capture_{timestamp:yyyy-MM-dd}_{timestamp:FFFFF}.png";
    }
}
