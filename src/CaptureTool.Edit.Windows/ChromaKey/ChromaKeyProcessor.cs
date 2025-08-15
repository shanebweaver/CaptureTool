using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System;
using System.Numerics;
using Windows.UI;

namespace CaptureTool.Edit.Windows.ChromaKey;

public class ChromaKeyProcessor
{
    private ICanvasImage? _lastSourceImage;
    private Color? _lastKeyColor;
    private float? _lastTolerance;
    private float? _lastSoftness;
    private CanvasBitmap? _cachedAlphaMask;
    private byte[]? _lastPixelBytes;
    private byte[]? _lastAlphaMaskBytes;
    private int _lastWidth = 0;
    private int _lastHeight = 0;

    public void DrawChromaKeyMaskedImage(
        CanvasDrawingSession ds,
        ICanvasImage sourceImage,
        Vector2 offset,
        Color keyColor,
        float tolerance,
        float softness = 0)
    {
        var alphaMaskBitmap = GetCachedAlphaMask(ds, sourceImage, keyColor, tolerance, softness);
        var composite = new CompositeEffect
        {
            Mode = CanvasComposite.DestinationIn,
            Sources = { sourceImage, alphaMaskBitmap }
        };

        ds.DrawImage(composite, offset);
    }

    private CanvasBitmap GetCachedAlphaMask(CanvasDrawingSession ds, ICanvasImage sourceImage, Color keyColor, float tolerance, float softness)
    {
        bool imageChanged = _lastSourceImage != sourceImage;
        bool paramsChanged = _lastKeyColor != keyColor || _lastTolerance != tolerance || _lastSoftness != softness;

        if (imageChanged)
        {
            var dpi = ds.Dpi;
            var size = sourceImage.GetBounds(ds);
            int width = (int)size.Width;
            int height = (int)size.Height;

            using var renderTarget = new CanvasRenderTarget(ds.Device, width, height, dpi);
            using (var rtDs = renderTarget.CreateDrawingSession())
            {
                rtDs.Clear(Colors.Transparent);
                rtDs.DrawImage(sourceImage);
            }

            _lastPixelBytes = renderTarget.GetPixelBytes();
            _lastWidth = width;
            _lastHeight = height;

            // Allocate new alpha mask bytes array
            _lastAlphaMaskBytes = new byte[width * height];

            // Generate new alpha mask bytes
            GenerateAlphaMaskFromBgraBytes(_lastPixelBytes, width, height, keyColor, tolerance, softness, _lastAlphaMaskBytes);

            // Create new CanvasBitmap from bytes
            _cachedAlphaMask = CanvasBitmap.CreateFromBytes(
                ds.Device,
                _lastAlphaMaskBytes,
                width,
                height,
                global::Windows.Graphics.DirectX.DirectXPixelFormat.A8UIntNormalized,
                dpi);
        }
        else if (paramsChanged)
        {
            if (_lastPixelBytes == null || _lastAlphaMaskBytes == null || _cachedAlphaMask == null)
                throw new InvalidOperationException("Pixel or alpha mask bytes missing.");

            // Regenerate alpha mask bytes in-place
            GenerateAlphaMaskFromBgraBytes(_lastPixelBytes, _lastWidth, _lastHeight, keyColor, tolerance, softness, _lastAlphaMaskBytes);

            // Update existing CanvasBitmap pixels on GPU without recreating bitmap
            _cachedAlphaMask.SetPixelBytes(_lastAlphaMaskBytes);
        }

        _lastSourceImage = sourceImage;
        _lastKeyColor = keyColor;
        _lastTolerance = tolerance;
        _lastSoftness = softness;

        return _cachedAlphaMask!;
    }

    private static void GenerateAlphaMaskFromBgraBytes(
        byte[] bgraBytes,
        int width,
        int height,
        Color keyColor,
        float tolerance,
        float softness,
        byte[] mask)
    {
        if (mask.Length < width * height)
            throw new ArgumentException("Mask buffer too small.", nameof(mask));

        RgbToHsv(keyColor.R, keyColor.G, keyColor.B, out float keyH, out float keyS, out float keyV);

        int stride = width * 4;

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * stride;
            int maskRowStart = y * width;

            for (int x = 0; x < width; x++)
            {
                int pixelIndex = rowStart + x * 4;

                byte b = bgraBytes[pixelIndex + 0];
                byte g = bgraBytes[pixelIndex + 1];
                byte r = bgraBytes[pixelIndex + 2];

                RgbToHsv(r, g, b, out float h, out float s, out float v);

                float dh = Math.Abs(h - keyH);
                if (dh > 180) dh = 360 - dh;

                float ds = Math.Abs(s - keyS);
                float dv = Math.Abs(v - keyV);

                float distance = dh / 180f + ds + dv;

                float alpha;
                float edge0 = tolerance;
                float edge1 = tolerance + softness;

                if (distance <= edge0)
                    alpha = 0f;
                else if (distance >= edge1)
                    alpha = 1f;
                else
                {
                    float t = (distance - edge0) / (edge1 - edge0);
                    alpha = t * t * (3f - 2f * t);
                }

                mask[maskRowStart + x] = (byte)(alpha * 255);
            }
        }
    }

    private static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;

        h = 0;
        if (delta == 0) h = 0;
        else if (max == rf) h = 60 * (((gf - bf) / delta) % 6);
        else if (max == gf) h = 60 * (((bf - rf) / delta) + 2);
        else if (max == bf) h = 60 * (((rf - gf) / delta) + 4);

        if (h < 0) h += 360;
        s = (max == 0) ? 0 : delta / max;
        v = max;
    }
}
