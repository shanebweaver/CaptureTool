using System;
using System.Drawing;

namespace CaptureTool.Capture;

public class MonitorCaptureResult
{
    public IntPtr HMonitor { get; set; }
    public required byte[] PixelBuffer { get; set; } // BGRA8
    public uint Dpi { get; set; }
    public float Scale => Dpi / 96f;
    public Rectangle MonitorBounds { get; set; }
    public Rectangle WorkAreaBounds { get; set; }

}
