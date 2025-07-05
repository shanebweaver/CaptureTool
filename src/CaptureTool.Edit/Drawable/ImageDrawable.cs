using CaptureTool.Common.Storage;
using System.Numerics;

namespace CaptureTool.Edit.Drawable;

public partial class ImageDrawable : IDrawable
{
    public Vector2 Offset { get; set; }

    public ImageFile FileName { get; set; }

    public ImageDrawable(Vector2 offset, ImageFile fileName)
    {
        Offset = offset;
        FileName = fileName;
    }
}
