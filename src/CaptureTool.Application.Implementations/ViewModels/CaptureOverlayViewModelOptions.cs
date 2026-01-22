using CaptureTool.Domains.Capture.Interfaces;
using System.Drawing;

namespace CaptureTool.Application.Implementations.ViewModels;

public readonly partial struct CaptureOverlayViewModelOptions(MonitorCaptureResult monitor, Rectangle area)
{
    public MonitorCaptureResult Monitor { get; } = monitor;
    public Rectangle Area { get; } = area;
}
