using CaptureTool.Domain.Analysis.Payloads;
using ZXing;
using ZXing.Common;

namespace CaptureTool.Infrastructure.Media;

/// <summary>Shared, local QR decoding for the editor and post-capture analysis.</summary>
public static class QrCodeDecoder
{
    public static IReadOnlyList<DecodedQrCode> Decode(byte[] bgraPixels, int width, int height,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (width <= 0 || height <= 0 || (long)width * height * 4 != bgraPixels.Length)
            throw new ArgumentException("Expected a tightly packed BGRA image.", nameof(bgraPixels));
        var source = new RGBLuminanceSource(bgraPixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true
            }
        };
        Result[] results = reader.DecodeMultiple(source) ?? [];
        cancellationToken.ThrowIfCancellationRequested();
        // DecodeMultiple does not apply the single-code reader's TryInverted option.
        // Scan both polarities so a mixed image can retain every code.
        results = [.. results, .. reader.DecodeMultiple(source.invert()) ?? []];
        cancellationToken.ThrowIfCancellationRequested();
        return results.Where(result => !string.IsNullOrEmpty(result.Text))
            .Select(result => (result.Text, Bounds: GetBounds(result.ResultPoints, width, height)))
            .Where(result => result.Bounds is { Width: > 0, Height: > 0 })
            .Select(result => new DecodedQrCode(result.Text, result.Bounds!))
            .Distinct().ToArray();
    }

    private static NormalizedBounds? GetBounds(ResultPoint[]? points, int width, int height)
    {
        if (points is null || points.Length == 0) return null;
        double left = points.Min(point => point.X), top = points.Min(point => point.Y);
        double right = points.Max(point => point.X), bottom = points.Max(point => point.Y);
        double padding = Math.Max(right - left, bottom - top) * 0.12;
        double x = Math.Clamp((left - padding) / width, 0, 1);
        double y = Math.Clamp((top - padding) / height, 0, 1);
        return new(x, y, Math.Clamp((right + padding) / width - x, 0, 1 - x),
            Math.Clamp((bottom + padding) / height - y, 0, 1 - y));
    }
}
