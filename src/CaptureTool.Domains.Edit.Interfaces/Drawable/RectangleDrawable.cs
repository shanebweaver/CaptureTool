using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domains.Edit.Interfaces.Drawable;

public sealed partial class RectangleDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; }
    public int StrokeWidth { get; set; }

    public RectangleDrawable(Vector2 offset, Size size, Color color, int strokeWidth)
    {
        Offset = offset;
        Size = size;
        Color = color;
        StrokeWidth = strokeWidth;
    }
}
