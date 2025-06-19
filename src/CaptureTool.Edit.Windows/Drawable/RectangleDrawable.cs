using Microsoft.Graphics.Canvas;
using System.Drawing;
using System.Numerics;
using Color = Windows.UI.Color;
using Rect = Windows.Foundation.Rect;

namespace CaptureTool.Edit.Windows.Drawable;

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
        Color color = Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
        drawingSession.DrawRectangle(rectangleRect, color, StrokeWidth);
    }
}
