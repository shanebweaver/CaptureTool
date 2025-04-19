using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public sealed partial class TextDrawable : IDrawable
{
    public Point Offset { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }

    public TextDrawable(Point position, string text, Color color)
    {
        Offset = position;
        Text = text;
        Color = color;
    }

    public void Draw(CanvasDrawingSession drawingSession)
    {
        Vector2 textPosition = new((float)Offset.X, (float)Offset.Y);
        drawingSession.DrawText(Text, textPosition, Color);
    }
}