using CaptureTool.Domain.Capture.Abstractions;
using System.Drawing;

namespace CaptureTool.Presentation.Features.CaptureOverlay;

public readonly partial struct CaptureOverlayViewModelOptions(MonitorCaptureResult monitor, Rectangle area)
{
    public MonitorCaptureResult Monitor { get; } = monitor;
    public Rectangle Area { get; } = area;
}
