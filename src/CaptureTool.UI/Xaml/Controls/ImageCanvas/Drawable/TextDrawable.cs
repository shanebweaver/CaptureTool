using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;

internal sealed partial class TextDrawable : IDrawable
{
    public Point Position { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }

    public TextDrawable(Point position, string text, Color color)
    {
        Position = position;
        Text = text;
        Color = color;
    }

    public void Draw(CanvasDrawingSession drawingSession, Rect sessionBounds)
    {
        Vector2 textPosition = new((float)(sessionBounds.X + Position.X), (float)(sessionBounds.Y + Position.Y));
        drawingSession.DrawText(Text, textPosition, Color);
    }
}