using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Abstractions.Drawable;

public sealed partial class TextDrawable : IDrawable
{
    public const string DefaultFontFamily = "Segoe UI";
    public const float DefaultFontSize = 24f;

    public Vector2 Offset { get; set; }
    public Size Size { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }
    public string FontFamily { get; set; }
    public float FontSize { get; set; }

    public TextDrawable()
        : this(Vector2.Zero, string.Empty, Color.Black)
    {
    }

    public TextDrawable(Vector2 position, string text, Color color)
        : this(position, text, color, DefaultFontFamily, DefaultFontSize)
    {
    }

    public TextDrawable(Vector2 position, string text, Color color, string fontFamily, float fontSize)
        : this(position, Size.Empty, text, color, fontFamily, fontSize)
    {
    }

    public TextDrawable(Vector2 position, Size size, string text, Color color, string fontFamily, float fontSize)
    {
        Offset = position;
        Size = size;
        Text = text;
        Color = color;
        FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? DefaultFontFamily : fontFamily;
        FontSize = fontSize > 0 ? fontSize : DefaultFontSize;
    }
}
