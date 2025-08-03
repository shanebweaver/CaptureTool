using System.Drawing;

namespace CaptureTool.Edit.ChromaKey;

public sealed partial class ChromaKeyColorOption
{
    public static ChromaKeyColorOption Empty { get; } = new(Color.Empty);

    public Color Color { get; }
    public string HexString { get; } = string.Empty;

    public bool IsEmpty => Color.IsEmpty;

    public ChromaKeyColorOption(Color color)
    {
        Color = color;

        if (color != Color.Empty)
        {
            HexString = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
