using CaptureTool.Capture;
using System.Drawing;

namespace CaptureTool.ViewModels;

public readonly partial struct CaptureOverlayViewModelOptions(MonitorCaptureResult monitor, Rectangle area)
{
    public MonitorCaptureResult Monitor { get; } = monitor;
    public Rectangle Area { get; } = area;
}
