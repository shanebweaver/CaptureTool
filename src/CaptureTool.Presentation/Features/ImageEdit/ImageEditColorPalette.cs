using System.Drawing;

namespace CaptureTool.Presentation.Features.ImageEdit;

internal static class ImageEditColorPalette
{
    public static readonly Color[] Drawables = [
        Color.Transparent,
        Color.FromArgb(31, 41, 55), // White
        Color.FromArgb(249, 250, 251), // Black
        Color.FromArgb(239, 68, 68), // Red
        Color.FromArgb(249, 115, 22),
        Color.FromArgb(245, 158, 11),
        Color.FromArgb(234, 179, 8),
        Color.FromArgb(132, 204, 22),
        Color.FromArgb(34, 197, 94),
        Color.FromArgb(20, 184, 166),
        Color.FromArgb(6, 182, 212),
        Color.FromArgb(59, 130, 246),
        Color.FromArgb(99, 102, 241),
        Color.FromArgb(139, 92, 246),
        Color.FromArgb(236, 72, 153),
        Color.FromArgb(244, 63, 94),
    ];

    public static Color ApplyOpacity(Color color, int opacityPercentage)
    {
        if (color.Equals(Color.Transparent))
        {
            return Color.Transparent;
        }

        int alpha = (int)Math.Round(Math.Clamp(opacityPercentage, 0, 100) / 100d * byte.MaxValue);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    public static int AlphaToOpacityPercentage(Color color)
    {
        return color.Equals(Color.Transparent)
            ? 100
            : (int)Math.Round(color.A / (double)byte.MaxValue * 100);
    }
}
