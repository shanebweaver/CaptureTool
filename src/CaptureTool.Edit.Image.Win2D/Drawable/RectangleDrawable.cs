using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

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

    public void Draw(CanvasDrawingSession drawingSession)
    {
        Rect rectangleRect = new(Offset.X, Offset.Y, Size.Width, Size.Height);
        drawingSession.DrawRectangle(rectangleRect, Color, StrokeWidth);
    }
}
