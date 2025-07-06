using CaptureTool.Common.Storage;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Edit.Drawable;

public partial class ImageDrawable : IDrawable
{
    public Vector2 Offset { get; set; }

    public ImageFile FileName { get; set; }

    public Size ImageSize { get; set; }

    public IImageEffect? ImageEffect { get; set; }

    public ImageDrawable(Vector2 offset, ImageFile fileName, Size imageSize, IImageEffect? imageEffect = null)
    {
        Offset = offset;
        FileName = fileName;
        ImageSize = imageSize;
        ImageEffect = imageEffect;
    }
}
