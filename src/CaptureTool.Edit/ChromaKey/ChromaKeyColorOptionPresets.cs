using System.Drawing;

namespace CaptureTool.Edit.ChromaKey;

public static partial class ChromaKeyColorOptionPresets
{
    public static ChromaKeyColorOption GreenScreenGreen { get; } = new(Color.FromArgb(4,244,4));
    public static ChromaKeyColorOption VividMalachite { get; } = new(Color.FromArgb(0, 204, 50));
    public static ChromaKeyColorOption SpringBud { get; } = new(Color.FromArgb(164, 255, 0));
    public static ChromaKeyColorOption BrightGreen { get; } = new(Color.FromArgb(102, 255, 1));
    public static ChromaKeyColorOption NeonGreen { get; } = new(Color.FromArgb(44, 231, 30));
    public static ChromaKeyColorOption PantoneGreen { get; } = new(Color.FromArgb(10, 175, 48));
    public static ChromaKeyColorOption NorthTexasGreen { get; } = new(Color.FromArgb(0, 151, 50));
    public static ChromaKeyColorOption RoyalBlue { get; } = new(Color.RoyalBlue);
    public static ChromaKeyColorOption ChromaGreen { get; } = new(Color.FromArgb(0, 177, 64));
    public static ChromaKeyColorOption TVGreen { get; } = new(Color.FromArgb(0, 255, 0));
    public static ChromaKeyColorOption DSLRGreen { get; } = new(Color.FromArgb(16, 180, 71));
    public static ChromaKeyColorOption ChromaBlue { get; } = new(Color.FromArgb(0, 71, 187));
    public static ChromaKeyColorOption TVBlue { get; } = new(Color.FromArgb(0, 0, 255));
    public static ChromaKeyColorOption HollywoodBlue { get; } = new(Color.FromArgb(29, 59, 163));

    public static ChromaKeyColorOption[] All { get; } = [
        ChromaGreen,
        TVGreen,
        DSLRGreen,
        PantoneGreen,
        ChromaBlue,
        TVBlue,
        HollywoodBlue
    ];
}
