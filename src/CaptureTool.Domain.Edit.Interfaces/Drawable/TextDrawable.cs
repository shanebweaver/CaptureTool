using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

public sealed partial class TextDrawable : IDrawable
{
    public Vector2 Offset { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }
    public string FontFamily { get; set; }
    public float FontSize { get; set; }

    public TextDrawable(Vector2 position, string text, Color color, string fontFamily = "Segoe UI", float fontSize = 16f)
    {
        Offset = position;
        Text = text;
        Color = color;
        FontFamily = fontFamily;
        FontSize = fontSize;
    }
}
