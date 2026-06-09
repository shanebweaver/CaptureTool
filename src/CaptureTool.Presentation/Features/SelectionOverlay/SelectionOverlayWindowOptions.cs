using CaptureTool.Domain.Capture;
using System.Drawing;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

public readonly struct SelectionOverlayWindowOptions
{
    public MonitorCaptureResult Monitor { get; }
    public IEnumerable<WindowInfo> MonitorWindows { get; }
    public CaptureOptions CaptureOptions { get; }

    public SelectionOverlayWindowOptions(MonitorCaptureResult monitor, IEnumerable<WindowInfo> monitorWindows, CaptureOptions options)
    {
        Monitor = monitor;
        MonitorWindows = monitorWindows;
        CaptureOptions = options;
    }
}
