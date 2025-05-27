namespace CaptureTool.Capture.Windows;

public class MonitorCaptureResult
{
    public int Width { get; set; }
    public int Height { get; set; }
    public required byte[] PixelBuffer { get; set; } // BGRA8
    public int Left { get; set; }
    public int Top { get; set; }
}
