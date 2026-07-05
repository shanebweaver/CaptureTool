using CaptureKit.Abstractions;
using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Infrastructure.Capture.Windows;

public static partial class MonitorCaptureHelper
{
    private static readonly IDisplayCaptureService DisplayCaptureService = new CaptureKit.Windows.DisplayCaptureService();

    public static Bitmap CombineMonitors(IList<MonitorCaptureResult> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor must be provided.", nameof(monitors));
        }

        return DisplayCaptureService.CombineDisplays([.. monitors.Select(ToDisplayCapture)]);
    }

    public static MonitorCaptureResult[] CaptureAllMonitors()
        => [.. DisplayCaptureService.CaptureDisplays().Select(ToMonitorCaptureResult)];

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
