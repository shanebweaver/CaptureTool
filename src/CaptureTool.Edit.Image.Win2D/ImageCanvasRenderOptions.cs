using System.Drawing;

namespace CaptureTool.Edit.Image.Win2D;

public sealed partial class ImageCanvasRenderOptions
{
    public RotateFlipType Orientation { get; set; }
    public Size CanvasSize { get; set; }
    public Windows.Foundation.Rect CropRect { get; set; }

    public ImageCanvasRenderOptions(RotateFlipType orientation, Size canvasSize, Windows.Foundation.Rect cropRect)
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
