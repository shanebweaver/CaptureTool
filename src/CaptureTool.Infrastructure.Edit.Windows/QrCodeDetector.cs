using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

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

    internal static IReadOnlyList<RecognizedQrCodeRegion> Detect(byte[] bgraPixels, int width, int height) =>
        CaptureTool.Infrastructure.Media.QrCodeDecoder.Decode(bgraPixels, width, height)
            .Select(code => new RecognizedQrCodeRegion(code.Value, new RectangleF(
                (float)(code.Bounds.X * width), (float)(code.Bounds.Y * height),
                (float)(code.Bounds.Width * width), (float)(code.Bounds.Height * height))))
            .ToArray();
}
