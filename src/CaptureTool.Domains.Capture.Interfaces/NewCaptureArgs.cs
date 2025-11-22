using System.Drawing;

namespace CaptureTool.Domains.Capture.Interfaces;

public readonly partial struct NewCaptureArgs
{
    public MonitorCaptureResult Monitor { get; }
    public Rectangle Area { get; }

    public NewCaptureArgs(MonitorCaptureResult monitor, Rectangle area)
    {
        Monitor = monitor;
        Area = area;
    }
}
