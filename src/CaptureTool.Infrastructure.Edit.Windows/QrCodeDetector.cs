using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

internal static class QrCodeDetector
{
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
