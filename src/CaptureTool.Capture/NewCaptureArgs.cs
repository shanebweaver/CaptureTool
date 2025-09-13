using System.Drawing;

namespace CaptureTool.Capture;

public sealed partial class NewCaptureArgs
{
    public MonitorCaptureResult Monitor { get; }
    public Rectangle Area { get; }

    public NewCaptureArgs(MonitorCaptureResult monitor, Rectangle area)
    {
        Monitor = monitor;
        Area = area;
    }
}
