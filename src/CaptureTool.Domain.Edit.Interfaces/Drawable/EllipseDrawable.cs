using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

public sealed partial class EllipseDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public Size Size { get; set; }
    public Color StrokeColor { get; set; }
    public Color FillColor { get; set; }
    public int StrokeWidth { get; set; }

    public EllipseDrawable(Vector2 offset, Size size, Color strokeColor, Color fillColor, int strokeWidth)
    {
        Offset = offset;
        Size = size;
        StrokeColor = strokeColor;
        FillColor = fillColor;
        StrokeWidth = strokeWidth;
    }
}
