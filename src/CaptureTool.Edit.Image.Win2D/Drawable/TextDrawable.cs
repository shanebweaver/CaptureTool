using System.Drawing;
using System.Numerics;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

public sealed partial class TextDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }

    public TextDrawable(Vector2 position, string text, Color color)
    {
        Offset = position;
        Text = text;
        Color = color;
    }

    public void Draw(CanvasDrawingSession drawingSession)
    {
        Vector2 textPosition = new(Offset.X, Offset.Y);
        Windows.UI.Color color = Windows.UI.Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
        drawingSession.DrawText(Text, textPosition, color);
    }
}