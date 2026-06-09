using System.Drawing;

namespace CaptureTool.Domain.Capture;

public readonly partial struct NewCaptureArgs
{
    public MonitorCaptureResult Monitor { get; }
    public Rectangle Area { get; }
    public CaptureType CaptureType { get; }
    public nint WindowHandle { get; }

    public NewCaptureArgs(MonitorCaptureResult monitor, Rectangle area, CaptureType captureType = CaptureType.Rectangle, nint windowHandle = 0)
    {
        Monitor = monitor;
        Area = area;
        CaptureType = captureType;
        WindowHandle = windowHandle;
    }
}
