using CaptureKit;
using CaptureTool.Domain.Capture;
using System.Drawing;
using System.Drawing.Imaging;

namespace CaptureTool.Infrastructure.Capture.Windows;

public class WindowsScreenCapture : IScreenCapture
{
    private readonly IDisplayCaptureService _displayCaptureService;

    public WindowsScreenCapture(IDisplayCaptureService displayCaptureService)
    {
        _displayCaptureService = displayCaptureService;
    }

    public Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor must be provided.", nameof(monitors));
        }

        return _displayCaptureService.CombineDisplays([.. monitors.Select(ToDisplayCapture)]);
    }

    public MonitorCaptureResult[] CaptureAllMonitors()
        => [.. _displayCaptureService.CaptureDisplays().Select(ToMonitorCaptureResult)];

    public void SaveImageToFile(Image image, string filePath)
    {
        image.Save(filePath, ImageFormat.Png);
    }

    public Bitmap CreateBitmapFromMonitorCaptureResult(MonitorCaptureResult monitor)
        => _displayCaptureService.CreateBitmap(ToDisplayCapture(monitor));

    public Bitmap CreateCroppedBitmap(Bitmap image, Rectangle area, float scale)
        => _displayCaptureService.CreateCroppedBitmap(image, area, scale);

    private static MonitorCaptureResult ToMonitorCaptureResult(DisplayCapture display)
        => new(
            display.MonitorHandle,
            display.PixelBuffer,
            display.DpiX,
            display.Bounds,
            display.WorkAreaBounds,
            display.IsPrimary);

    private static DisplayCapture ToDisplayCapture(MonitorCaptureResult monitor)
        => new(
            monitor.HMonitor,
            monitor.PixelBuffer,
            monitor.Dpi,
            monitor.Dpi,
            monitor.MonitorBounds,
            monitor.WorkAreaBounds,
            monitor.IsPrimary);
}
