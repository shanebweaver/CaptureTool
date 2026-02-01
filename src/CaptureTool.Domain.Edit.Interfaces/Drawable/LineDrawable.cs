using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

public sealed partial class LineDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public Vector2 EndPoint { get; set; }
    public Color StrokeColor { get; set; }
    public int StrokeWidth { get; set; }

    public LineDrawable(Vector2 offset, Vector2 endPoint, Color strokeColor, int strokeWidth)
    {
        Offset = offset;
        EndPoint = endPoint;
        StrokeColor = strokeColor;
        StrokeWidth = strokeWidth;
    }
}
