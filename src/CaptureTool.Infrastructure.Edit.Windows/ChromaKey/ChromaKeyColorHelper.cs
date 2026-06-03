using Microsoft.Graphics.Canvas;
using System.Drawing;

namespace CaptureTool.Infrastructure.Edit.Windows.ChromaKey;

public static partial class ChromaKeyColorHelper
{
    private const int MaxSampledPixels = 1_000_000;

    public static async Task<Color[]> GetTopColorsAsync(string fileName, uint count = 3, byte quantizeStep = 8)
    {
        var device = CanvasDevice.GetSharedDevice();
        using var bitmap = await CanvasBitmap.LoadAsync(device, fileName);

        var pixelBytes = bitmap.GetPixelBytes();
        int pixelCount = pixelBytes.Length / 4;
        int pixelStep = Math.Max(1, pixelCount / MaxSampledPixels);

        return await Task.Run(() => GetTopColors(pixelBytes, pixelStep, count, quantizeStep));
    }

    private static Color[] GetTopColors(byte[] pixelBytes, int pixelStep, uint count, byte quantizeStep)
    {
        var colorCounts = new Dictionary<Color, int>();
        int byteStep = pixelStep * 4;

        for (int i = 0; i < pixelBytes.Length; i += byteStep)
        {
            byte b = pixelBytes[i + 0];
            byte g = pixelBytes[i + 1];
            byte r = pixelBytes[i + 2];
            byte a = pixelBytes[i + 3];

            if (a == 0) // Skip fully transparent
                continue;

            // Quantize RGB to reduce variations
            r = Quantize(r, quantizeStep);
            g = Quantize(g, quantizeStep);
            b = Quantize(b, quantizeStep);

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

    private static byte Quantize(byte value, byte quantizeStep)
    {
        if (quantizeStep == 0)
        {
            return value;
        }

        double quantized = Math.Round(value / (double)quantizeStep) * quantizeStep;
        return (byte)Math.Clamp(quantized, byte.MinValue, byte.MaxValue);
    }
}
