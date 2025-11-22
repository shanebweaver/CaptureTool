using CaptureTool.Services.Interfaces.Storage;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domains.Edit.Interfaces.Drawable;

public partial class ImageDrawable : IDrawable
{
    public Vector2 Offset { get; set; }

    public IFile File { get; set; }

    public Size ImageSize { get; set; }

    public IImageEffect? ImageEffect { get; set; }

    public ImageDrawable(Vector2 offset, IFile file, Size imageSize, IImageEffect? imageEffect = null)
    {
        Offset = offset;
        File = file;
        ImageSize = imageSize;
        ImageEffect = imageEffect;
    }
}
