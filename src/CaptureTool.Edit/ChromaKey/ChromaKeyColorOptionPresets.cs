using System.Drawing;

namespace CaptureTool.Edit.ChromaKey;

public static partial class ChromaKeyColorOptionPresets
{
    public static ChromaKeyColorOption Green1 { get; } = new(Color.LimeGreen);
    public static ChromaKeyColorOption Blue1 { get; } = new(Color.RoyalBlue);

    public static ChromaKeyColorOption[] All { get; } = [
        Green1,
        Blue1
    ];
}
