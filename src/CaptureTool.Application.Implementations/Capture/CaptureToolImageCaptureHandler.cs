using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.TaskEnvironment;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using System.Drawing;

namespace CaptureTool.Application.Implementations.Capture;

public partial class CaptureToolImageCaptureHandler : IImageCaptureHandler
{
    private readonly IClipboardService _clipboardService;
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly IScreenCapture _screenCapture;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;

    public event EventHandler<IImageFile>? NewImageCaptured;

    public CaptureToolImageCaptureHandler(
        IClipboardService clipboardService,
        IStorageService storageService,
        ISettingsService settingsService,
        IScreenCapture screenCapture,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService)
    {
        _clipboardService = clipboardService;
        _storageService = storageService;
        _settingsService = settingsService;
        _screenCapture = screenCapture;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;
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
                _telemetryService.ActivityError("AutoCopyImageAsync", e);
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

                File.Copy(tempFilePath, newFilePath, true);
            }
            catch (Exception e)
            {
                _telemetryService.ActivityError("AutoSaveImage", e);
            }
        });
    }

    private static string GetNewCaptureFileName()
    {
        DateTime timestamp = DateTime.Now;
        return $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.png";
    }
}
