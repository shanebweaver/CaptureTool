using CaptureTool.Core.Settings;
using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Settings;
using Microsoft.Windows.Storage;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolImageCaptureHandler : IImageCaptureHandler
{
    private readonly ISettingsService _settingsService;

    public CaptureToolImageCaptureHandler(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    
    public ImageFile PerformAllScreensCapture()
    {
        var monitors = MonitorCaptureHelper.CaptureAllMonitors();
        return PerformMultiMonitorImageCapture(monitors);
    }

    public ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors)
    {
        Bitmap combined = MonitorCaptureHelper.CombineMonitors(monitors);
        try
        {
            var tempPath = Path.Combine(
                ApplicationData.GetDefault().TemporaryPath,
                $"capture_{Guid.NewGuid()}.png"
            );
            combined.Save(tempPath, ImageFormat.Png);

            ImageFile imageFile = new(tempPath);
            TryAutoSaveImage(imageFile);
            _ = TryAutoCopyImageAsync(imageFile);

            return imageFile;
        }
        finally
        {
            combined.Dispose();
        }
    }

    public ImageFile PerformImageCapture(NewCaptureArgs args)
    {
        var monitor = args.Monitor;
        var area = args.Area;
        var monitorBounds = monitor.MonitorBounds;

        // Create a bitmap for the full monitor
        using var fullBmp = new Bitmap(monitorBounds.Width, monitorBounds.Height, PixelFormat.Format32bppArgb);
        var bmpData = fullBmp.LockBits(
            new Rectangle(0, 0, monitorBounds.Width, monitorBounds.Height),
            ImageLockMode.WriteOnly,
            fullBmp.PixelFormat
        );

        try
        {
            Marshal.Copy(monitor.PixelBuffer, 0, bmpData.Scan0, monitor.PixelBuffer.Length);
        }
        finally
        {
            fullBmp.UnlockBits(bmpData);
        }

        var tempPath = Path.Combine(
            ApplicationData.GetDefault().TemporaryPath,
            $"capture_{Guid.NewGuid()}.png"
        );

        // Crop to the selected area
        float scale = monitor.Scale; int cropX = (int)Math.Round((area.Left) * scale);
        int cropY = (int)Math.Round((area.Top) * scale);
        int cropWidth = (int)Math.Round(area.Width * scale);
        int cropHeight = (int)Math.Round(area.Height * scale);

        // Ensure cropping stays within image bounds
        cropX = Math.Clamp(cropX, 0, fullBmp.Width - 1);
        cropY = Math.Clamp(cropY, 0, fullBmp.Height - 1);
        cropWidth = Math.Clamp(cropWidth, 1, fullBmp.Width - cropX);
        cropHeight = Math.Clamp(cropHeight, 1, fullBmp.Height - cropY);

        var cropRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
        using var croppedBmp = fullBmp.Clone(cropRect, fullBmp.PixelFormat);
        croppedBmp.Save(tempPath, ImageFormat.Png);

        var imageFile = new ImageFile(tempPath);
        TryAutoSaveImage(imageFile);
        _ = TryAutoCopyImageAsync(imageFile);

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

            // Load the file
            global::Windows.Storage.StorageFile file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(imageFile.FilePath);

            // Open the file as a stream
            using IRandomAccessStream stream = await file.OpenAsync(global::Windows.Storage.FileAccessMode.Read);

            // Decode the bitmap
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to a compatible format (Clipboard requires BGRA8 with premultiplied alpha)
            SoftwareBitmap converted = SoftwareBitmap.Convert(
                softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied
            );

            // Encode to PNG into a stream
            InMemoryRandomAccessStream inMemoryStream = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryStream);
            encoder.SetSoftwareBitmap(converted);
            await encoder.FlushAsync();

            // Prepare clipboard content
            DataPackage dataPackage = new();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(inMemoryStream));
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();

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

            string screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = GetDefaultScreenshotsFolderPath();
            }

            string tempFilePath = imageFile.FilePath;
            string fileName = Path.GetFileName(tempFilePath);
            string newFilePath = Path.Combine(screenshotsFolder, fileName);

            File.Copy(tempFilePath, newFilePath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetDefaultScreenshotsFolderPath()
    {
        return global::Windows.Storage.KnownFolders.SavedPictures.Path;
    }
}
