using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System.Numerics;
using Windows.UI;

namespace CaptureTool.Domain.Edit.Windows.ChromaKey;

public class ChromaKeyProcessor
{
    private const string ShaderResourceName = "CaptureTool.Domain.Edit.Windows.ChromaKey.ChromaKeyShader.bin";
    private const string KeyColorShaderProperty = "keyColor";
    private const string ToleranceShaderProperty = "tolerance";
    private const string SoftnessShaderProperty = "softness";

    private static readonly Lazy<byte[]> GpuShaderBytecode = new(LoadGpuShaderBytecode);

    private CanvasDevice? _lastDevice;
    private ICanvasImage? _lastSourceImage;
    private Color? _lastKeyColor;
    private float? _lastTolerance;
    private float? _lastSoftness;
    private CanvasBitmap? _cachedAlphaMask;
    private byte[]? _lastAlphaMaskBytes;
    private float[]? _lastHueValues;
    private float[]? _lastSaturationValues;
    private float[]? _lastValueValues;
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
        if (TryDrawGpuChromaKey(ds, sourceImage, offset, keyColor, tolerance, softness))
        {
            return;
        }

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
        bool imageChanged = _lastSourceImage != sourceImage || _lastDevice != ds.Device;
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

            byte[] pixelBytes = renderTarget.GetPixelBytes();
            _lastWidth = width;
            _lastHeight = height;
            _lastHueValues = new float[width * height];
            _lastSaturationValues = new float[width * height];
            _lastValueValues = new float[width * height];

            CacheHsvValues(pixelBytes, width, height, _lastHueValues, _lastSaturationValues, _lastValueValues);

            _lastAlphaMaskBytes = new byte[width * height];
            GenerateAlphaMaskFromHsvValues(_lastHueValues, _lastSaturationValues, _lastValueValues, width, height, keyColor, tolerance, softness, _lastAlphaMaskBytes);

            _cachedAlphaMask?.Dispose();
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
            if (_lastHueValues == null || _lastSaturationValues == null || _lastValueValues == null || _lastAlphaMaskBytes == null || _cachedAlphaMask == null)
            {
                throw new InvalidOperationException("Pixel or alpha mask bytes missing.");
            }

            GenerateAlphaMaskFromHsvValues(_lastHueValues, _lastSaturationValues, _lastValueValues, _lastWidth, _lastHeight, keyColor, tolerance, softness, _lastAlphaMaskBytes);
            _cachedAlphaMask.SetPixelBytes(_lastAlphaMaskBytes);
        }

        _lastDevice = ds.Device;
        _lastSourceImage = sourceImage;
        _lastKeyColor = keyColor;
        _lastTolerance = tolerance;
        _lastSoftness = softness;

        return _cachedAlphaMask!;
    }

    private static bool TryDrawGpuChromaKey(
        CanvasDrawingSession ds,
        ICanvasImage sourceImage,
        Vector2 offset,
        Color keyColor,
        float tolerance,
        float softness)
    {
        try
        {
            using var effect = new PixelShaderEffect(GpuShaderBytecode.Value)
            {
                Source1 = sourceImage
            };

            if (!effect.IsSupported(ds.Device))
            {
                return false;
            }

            effect.Properties[KeyColorShaderProperty] = new Vector4(
                keyColor.R / 255f,
                keyColor.G / 255f,
                keyColor.B / 255f,
                keyColor.A / 255f);
            effect.Properties[ToleranceShaderProperty] = tolerance;
            effect.Properties[SoftnessShaderProperty] = softness;

            ds.DrawImage(effect, offset);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[] LoadGpuShaderBytecode()
    {
        using Stream? stream = typeof(ChromaKeyProcessor).Assembly.GetManifestResourceStream(ShaderResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"The embedded chroma key shader resource '{ShaderResourceName}' could not be loaded.");
        }

        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static void CacheHsvValues(
        byte[] bgraBytes,
        int width,
        int height,
        float[] hueValues,
        float[] saturationValues,
        float[] valueValues)
    {
        int pixelCount = width * height;
        if (hueValues.Length < pixelCount || saturationValues.Length < pixelCount || valueValues.Length < pixelCount)
        {
            throw new ArgumentException("HSV buffers are too small.");
        }

        int stride = width * 4;
        Parallel.For(0, height, y =>
        {
            int rowStart = y * stride;
            int valueRowStart = y * width;

            for (int x = 0; x < width; x++)
            {
                int pixelIndex = rowStart + x * 4;
                int valueIndex = valueRowStart + x;

                byte b = bgraBytes[pixelIndex + 0];
                byte g = bgraBytes[pixelIndex + 1];
                byte r = bgraBytes[pixelIndex + 2];

                RgbToHsv(r, g, b, out float h, out float s, out float v);
                hueValues[valueIndex] = h;
                saturationValues[valueIndex] = s;
                valueValues[valueIndex] = v;
            }
        });
    }

    private static void GenerateAlphaMaskFromHsvValues(
        float[] hueValues,
        float[] saturationValues,
        float[] valueValues,
        int width,
        int height,
        Color keyColor,
        float tolerance,
        float softness,
        byte[] mask)
    {
        if (mask.Length < width * height)
        {
            throw new ArgumentException("Mask buffer too small.", nameof(mask));
        }

        RgbToHsv(keyColor.R, keyColor.G, keyColor.B, out float keyH, out float keyS, out float keyV);

        Parallel.For(0, height, y =>
        {
            int maskRowStart = y * width;

            for (int x = 0; x < width; x++)
            {
                int pixelIndex = maskRowStart + x;
                float h = hueValues[pixelIndex];
                float s = saturationValues[pixelIndex];
                float v = valueValues[pixelIndex];

                float dh = Math.Abs(h - keyH);
                if (dh > 180)
                {
                    dh = 360 - dh;
                }

                float ds = Math.Abs(s - keyS);
                float dv = Math.Abs(v - keyV);
                float distance = dh / 180f + ds + dv;
                float alpha = CalculateAlpha(distance, tolerance, softness);

                mask[pixelIndex] = (byte)(alpha * 255);
            }
        });
    }

    private static float CalculateAlpha(float distance, float tolerance, float softness)
    {
        float edge0 = tolerance;
        float edge1 = tolerance + softness;

        if (distance <= edge0)
        {
            return 0f;
        }

        if (distance >= edge1)
        {
            return 1f;
        }

        float t = (distance - edge0) / (edge1 - edge0);
        return t * t * (3f - 2f * t);
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
