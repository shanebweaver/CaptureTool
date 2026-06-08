using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Drawable;

public sealed partial class TextDrawable : IDrawable
{
    public static readonly Size DefaultSize = new(240, 80);
    public const string DefaultFontFamily = "Segoe UI";
    public const float DefaultFontSize = 24f;

    public Vector2 Offset { get; set; }
    public Size Size { get; set; }
    public string Text { get; set; }
    public Color Color { get; set; }
    public Color BackgroundColor { get; set; }
    public string FontFamily { get; set; }
    public float FontSize { get; set; }

    public TextDrawable(Vector2 position, string text, Color color)
        : this(position, DefaultSize, text, color, Color.Transparent, DefaultFontFamily, DefaultFontSize)
    {
    }

    public TextDrawable(
        Vector2 position,
        Size size,
        string text,
        Color color,
        Color backgroundColor,
        string fontFamily,
        float fontSize)
    {
        Offset = position;
        Size = size;
        Text = text;
        Color = color;
        BackgroundColor = backgroundColor;
        FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? DefaultFontFamily : fontFamily;
        FontSize = fontSize > 0 ? fontSize : DefaultFontSize;
    }
}
