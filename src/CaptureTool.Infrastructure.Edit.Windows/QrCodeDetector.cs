using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using ZXing;
using ZXing.Common;

namespace CaptureTool.Infrastructure.Edit.Windows;

internal static class QrCodeDetector
{
    internal static bool ShouldExcludeText(
        RectangleF textBounds,
        IReadOnlyList<RecognizedQrCodeRegion> qrCodes)
    {
        if (textBounds.Width <= 0 || textBounds.Height <= 0)
        {
            return false;
        }

        PointF textCenter = new(
            textBounds.Left + (textBounds.Width / 2),
            textBounds.Top + (textBounds.Height / 2));
        foreach (RecognizedQrCodeRegion qrCode in qrCodes)
        {
            RectangleF exclusionBounds = qrCode.Bounds;
            float padding = Math.Clamp(
                Math.Min(exclusionBounds.Width, exclusionBounds.Height) * 0.04f,
                2,
                10);
            exclusionBounds.Inflate(padding, padding);
            if (exclusionBounds.Contains(textCenter))
            {
                return true;
            }

            RectangleF overlap = RectangleF.Intersect(textBounds, exclusionBounds);
            if (overlap.Width * overlap.Height >= textBounds.Width * textBounds.Height * 0.35f)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<RecognizedQrCodeRegion> Detect(SoftwareBitmap bitmap)
    {
        int byteCount = checked(bitmap.PixelWidth * bitmap.PixelHeight * 4);
        var buffer = new global::Windows.Storage.Streams.Buffer((uint)byteCount);
        bitmap.CopyToBuffer(buffer);
        byte[] pixels = new byte[byteCount];
        using DataReader dataReader = DataReader.FromBuffer(buffer);
        dataReader.ReadBytes(pixels);

        return Detect(pixels, bitmap.PixelWidth, bitmap.PixelHeight);
    }

    internal static IReadOnlyList<RecognizedQrCodeRegion> Detect(byte[] bgraPixels, int width, int height)
    {
        var source = new RGBLuminanceSource(
            bgraPixels,
            width,
            height,
            RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true,
                TryInverted = true
            }
        };

        Result[] results = reader.DecodeMultiple(source) ?? [];
        return results
            .Where(result => !string.IsNullOrWhiteSpace(result.Text))
            .Select(result => new RecognizedQrCodeRegion(result.Text, GetBounds(result.ResultPoints)))
            .Where(region => region.Bounds.Width > 0 && region.Bounds.Height > 0)
            .GroupBy(region => (region.Value, region.Bounds))
            .Select(group => group.First())
            .ToArray();
    }

    private static RectangleF GetBounds(ResultPoint[]? points)
    {
        if (points is null || points.Length == 0)
        {
            return RectangleF.Empty;
        }

        float left = points.Min(point => point.X);
        float top = points.Min(point => point.Y);
        float right = points.Max(point => point.X);
        float bottom = points.Max(point => point.Y);
        RectangleF bounds = RectangleF.FromLTRB(left, top, right, bottom);
        float padding = Math.Max(bounds.Width, bounds.Height) * 0.12f;
        bounds.Inflate(padding, padding);
        return RectangleF.FromLTRB(
            Math.Max(0, bounds.Left),
            Math.Max(0, bounds.Top),
            bounds.Right,
            bounds.Bottom);
    }
}
