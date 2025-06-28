using System.Drawing;

namespace CaptureTool.Edit;

public sealed partial class ImageCanvasRenderOptions
{
    public Orientation Orientation { get; set; }
    public Size CanvasSize { get; set; }
    public Rectangle CropRect { get; set; }
    public float Dpi { get; set; } = 96f;

    public ImageCanvasRenderOptions(Orientation orientation, Size canvasSize, Rectangle cropRect)
    {
        Orientation = orientation;
        CanvasSize = canvasSize;
        CropRect = cropRect;
    }

    public bool IsTurned =>
        Orientation == Orientation.Rotate90FlipNone ||
        Orientation == Orientation.Rotate270FlipNone ||
        Orientation == Orientation.Rotate90FlipX ||
        Orientation == Orientation.Rotate270FlipX;
}
