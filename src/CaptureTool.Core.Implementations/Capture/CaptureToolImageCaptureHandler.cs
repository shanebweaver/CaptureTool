using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Clipboard;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Drawing;

namespace CaptureTool.Core.Implementations.Capture;

public partial class CaptureToolImageCaptureHandler : IImageCaptureHandler
{
    private readonly IClipboardService _clipboardService;
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly IScreenCapture _screenCapture;

    public event EventHandler<IImageFile>? NewImageCaptured;

    public CaptureToolImageCaptureHandler(
        IClipboardService clipboardService,
        IStorageService storageService,
        ISettingsService settingsService,
        IScreenCapture screenCapture)
    {
        _clipboardService = clipboardService;
        _storageService = storageService;
        _settingsService = settingsService;
        _screenCapture = screenCapture;
    }
    
    public ImageFile PerformAllScreensCapture()
    {
        MonitorCaptureResult[] monitors = _screenCapture.CaptureAllMonitors();
        return PerformMultiMonitorImageCapture(monitors);
    }

    public ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors)
    {
        Image combined = _screenCapture.CombineMonitors(monitors);

        DateTime timestamp = DateTime.Now;
        string fileName = $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.png";
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            fileName
        );

        _screenCapture.SaveImageToFile(combined, tempPath);

        ImageFile imageFile = new(tempPath);
        TryAutoSaveImage(imageFile);
        _ = TryAutoCopyImageAsync(imageFile);

        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
    }

    public ImageFile PerformImageCapture(NewCaptureArgs args)
    {
        MonitorCaptureResult monitor = args.Monitor;
        Rectangle area = args.Area;
        DateTime timestamp = DateTime.Now;
        string fileName = $"Capture {timestamp:yyyy-MM-dd} {timestamp:FFFFF}.png";
        string tempPath = Path.Combine(
            _storageService.GetApplicationTemporaryFolderPath(),
            fileName
        );

        using Bitmap image = _screenCapture.CreateBitmapFromMonitorCaptureResult(monitor);
        using Bitmap cropped = _screenCapture.CreateCroppedBitmap(image, area, monitor.Scale);
        _screenCapture.SaveImageToFile(cropped, tempPath);

        var imageFile = new ImageFile(tempPath);
        TryAutoSaveImage(imageFile);
        _ = TryAutoCopyImageAsync(imageFile);

        NewImageCaptured?.Invoke(this, imageFile);
        return imageFile;
    }

    private async Task<bool> TryAutoCopyImageAsync(ImageFile imageFile)
    {
        try
        {
            bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            if (!autoCopy)
            {
                return false;
            }

            ClipboardFile clipboardFile = new(imageFile.FilePath);
            await _clipboardService.CopyBitmapAsync(clipboardFile);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryAutoSaveImage(ImageFile imageFile)
    {
        try
        {
            bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);
            if (!autoSave)
            {
                return false;
            }

            string screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
            }

            string tempFilePath = imageFile.FilePath;
            string fileName = Path.GetFileName(tempFilePath);
            string newFilePath = Path.Combine(screenshotsFolder, $"capture_{Guid.NewGuid()}.png");

            File.Copy(tempFilePath, newFilePath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
