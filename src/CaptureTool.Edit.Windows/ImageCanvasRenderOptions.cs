using System.Drawing;

namespace CaptureTool.Edit.Windows;

public sealed partial class ImageCanvasRenderOptions
{
    public RotateFlipType Orientation { get; set; }
    public Size CanvasSize { get; set; }
    public Rectangle CropRect { get; set; }
    public float Dpi { get; set; } = 96f;

    public ImageCanvasRenderOptions(RotateFlipType orientation, Size canvasSize, Rectangle cropRect)
    {
        Orientation = orientation;
        CanvasSize = canvasSize;
        CropRect = cropRect;
    }

    public bool IsTurned =>
        Orientation == RotateFlipType.Rotate90FlipNone ||
        Orientation == RotateFlipType.Rotate270FlipNone ||
        Orientation == RotateFlipType.Rotate90FlipX ||
        Orientation == RotateFlipType.Rotate270FlipX;
}
