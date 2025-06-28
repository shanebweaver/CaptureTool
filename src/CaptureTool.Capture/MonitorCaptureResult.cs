namespace CaptureTool.Capture;

public class MonitorCaptureResult
{
    public int Width { get; set; }
    public int Height { get; set; }
    public required byte[] PixelBuffer { get; set; } // BGRA8
    public int Left { get; set; }
    public int Top { get; set; }
    public uint Dpi { get; set; }
    public float Scale => Dpi / 96f;
}
