using CaptureTool.Domains.Capture.Interfaces;
using System.Drawing;

namespace CaptureTool.Application.Implementations.ViewModels;

public readonly struct SelectionOverlayWindowOptions
{
    public MonitorCaptureResult Monitor { get; }
    public IEnumerable<Rectangle> MonitorWindows { get; }
    public CaptureOptions CaptureOptions { get; }

    public SelectionOverlayWindowOptions(MonitorCaptureResult monitor, IEnumerable<Rectangle> monitorWindows, CaptureOptions options)
    {
        Monitor = monitor;
        MonitorWindows = monitorWindows;
        CaptureOptions = options;
    }
}
