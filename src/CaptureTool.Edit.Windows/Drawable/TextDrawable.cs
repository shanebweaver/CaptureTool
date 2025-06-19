using Microsoft.Graphics.Canvas;
using System.Numerics;
using Color = Windows.UI.Color;

namespace CaptureTool.Edit.Windows.Drawable;

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
        Color color = Color.FromArgb(Color.A, Color.R, Color.G, Color.B);
        drawingSession.DrawText(Text, textPosition, color);
    }
}