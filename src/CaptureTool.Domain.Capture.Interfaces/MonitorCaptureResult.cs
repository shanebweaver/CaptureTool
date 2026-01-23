using System.Drawing;

namespace CaptureTool.Domain.Capture.Interfaces;

public readonly partial struct MonitorCaptureResult
{
    public IntPtr HMonitor { get; }
    public byte[] PixelBuffer { get; } // BGRA8
    public uint Dpi { get; }
    public float Scale => Dpi / 96f;
    public Rectangle MonitorBounds { get; }
    public Rectangle WorkAreaBounds { get; }
    public bool IsPrimary { get; }

    public MonitorCaptureResult(nint hMonitor, byte[] pixelBuffer, uint dpi, Rectangle monitorBounds, Rectangle workAreaBounds, bool isPrimary)
    {
        HMonitor = hMonitor;
        PixelBuffer = pixelBuffer;
        Dpi = dpi;
        MonitorBounds = monitorBounds;
        WorkAreaBounds = workAreaBounds;
        IsPrimary = isPrimary;
    }
}
