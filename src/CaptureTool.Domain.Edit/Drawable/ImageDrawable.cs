using CaptureTool.Domain.FileSystem;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Drawable;

public partial class ImageDrawable : IDrawable
{
    public Vector2 Offset { get; set; }

    public ImageFile File { get; set; }

    public Size ImageSize { get; set; }

    public IImageEffect? ImageEffect { get; set; }

    public ImageDrawable(Vector2 offset, ImageFile file, Size imageSize, IImageEffect? imageEffect = null)
    {
        Offset = offset;
        File = file;
        ImageSize = imageSize;
        ImageEffect = imageEffect;
    }
}
