using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

public sealed partial class RectangleDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; }
    public Color StrokeColor { get; set; }
    public Color FillColor { get; set; }
    public int StrokeWidth { get; set; }

    public RectangleDrawable(Vector2 offset, Size size, Color color, int strokeWidth)
    {
        Offset = offset;
        Size = size;
        Color = color;
        StrokeColor = color;
        FillColor = Color.Transparent;
        StrokeWidth = strokeWidth;
    }

    public RectangleDrawable(Vector2 offset, Size size, Color strokeColor, Color fillColor, int strokeWidth)
    {
        Offset = offset;
        Size = size;
        Color = strokeColor; // For backward compatibility
        StrokeColor = strokeColor;
        FillColor = fillColor;
        StrokeWidth = strokeWidth;
    }
}
