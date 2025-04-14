using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;

internal sealed partial class RectangleDrawable : IDrawable
{
    public Point Position { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; }
    public int StrokeWidth { get; set; }

    public RectangleDrawable(Point position, Size size, Color color, int strokeWidth)
    {
        Position = position;
        Size = size;
        Color = color;
        StrokeWidth = strokeWidth;
    }

    public void Draw(CanvasDrawingSession drawingSession, Rect sessionBounds)
    {
        Rect rectangleRect = new(sessionBounds.X + Position.X, sessionBounds.Y + Position.Y, Size.Width, Size.Height);
        drawingSession.DrawRectangle(rectangleRect, Color, StrokeWidth);
    }
}
