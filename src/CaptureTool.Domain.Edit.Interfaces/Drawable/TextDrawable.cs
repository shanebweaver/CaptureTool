using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

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
}
