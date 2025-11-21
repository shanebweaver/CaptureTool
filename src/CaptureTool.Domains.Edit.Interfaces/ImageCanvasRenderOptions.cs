using System.Drawing;

namespace CaptureTool.Domains.Edit.Interfaces;

public sealed partial class ImageCanvasRenderOptions
{
    public ImageOrientation Orientation { get; set; }
    public Size CanvasSize { get; set; }
    public Rectangle CropRect { get; set; }
    public float Dpi { get; set; } = 96f;

    public ImageCanvasRenderOptions(ImageOrientation orientation, Size canvasSize, Rectangle cropRect)
    {
        Orientation = orientation;
        CanvasSize = canvasSize;
        CropRect = cropRect;
    }
}
