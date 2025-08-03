using System.Drawing;
using static System.Net.Mime.MediaTypeNames;

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

    public static ChromaKeyColorOption[] All { get; } = [
        SpringBud,
        BrightGreen,
        GreenScreenGreen,
        NeonGreen,
        VividMalachite,
        PantoneGreen,
        NorthTexasGreen,
        RoyalBlue
    ];
}
