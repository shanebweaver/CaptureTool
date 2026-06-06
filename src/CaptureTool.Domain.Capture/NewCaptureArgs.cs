using System.Drawing;

namespace CaptureTool.Domain.Capture;

public readonly partial struct NewCaptureArgs
{
    public MonitorCaptureResult Monitor { get; }
    public Rectangle Area { get; }

    public NewCaptureArgs(MonitorCaptureResult monitor, Rectangle area)
    {
        Monitor = monitor;
        Area = area;
    }

    public Rectangle GetPhysicalCaptureArea()
    {
        float scale = Monitor.Scale;
        int monitorWidth = Monitor.MonitorBounds.Width;
        int monitorHeight = Monitor.MonitorBounds.Height;

        int left = Math.Clamp((int)Math.Round(Area.Left * scale), 0, monitorWidth);
        int top = Math.Clamp((int)Math.Round(Area.Top * scale), 0, monitorHeight);
        int right = Math.Clamp((int)Math.Round(Area.Right * scale), left, monitorWidth);
        int bottom = Math.Clamp((int)Math.Round(Area.Bottom * scale), top, monitorHeight);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
