using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace CaptureTool.Domains.Edit.Implementations.Windows.ChromaKey;

public static partial class ChromaKeyColorHelper
{
    public static async Task<Color[]> GetTopColorsAsync(string fileName, uint count = 3, byte quantizeStep = 8)
    {
        var device = CanvasDevice.GetSharedDevice();
        using var bitmap = await CanvasBitmap.LoadAsync(device, fileName);

        var pixelBytes = bitmap.GetPixelBytes();
        var colorCounts = new Dictionary<Color, int>();

        for (int i = 0; i < pixelBytes.Length; i += 4)
        {
            byte b = pixelBytes[i + 0];
            byte g = pixelBytes[i + 1];
            byte r = pixelBytes[i + 2];
            byte a = pixelBytes[i + 3];

            if (a == 0) // Skip fully transparent
                continue;

            // Quantize RGB to reduce variations
            r = (byte)(Math.Round(r / (double)quantizeStep) * quantizeStep);
            g = (byte)(Math.Round(g / (double)quantizeStep) * quantizeStep);
            b = (byte)(Math.Round(b / (double)quantizeStep) * quantizeStep);

            var color = Color.FromArgb(255, r, g, b);

            if (colorCounts.TryGetValue(color, out int existing))
                colorCounts[color] = existing + 1;
            else
                colorCounts[color] = 1;
        }

        return [.. colorCounts
            .OrderByDescending(kv => kv.Value)
            .Take((int)count)
            .Select(kv => kv.Key)];
    }
}