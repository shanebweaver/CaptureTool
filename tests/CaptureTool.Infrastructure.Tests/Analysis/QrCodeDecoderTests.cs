using CaptureTool.Infrastructure.Media;
using ZXing;
using ZXing.Common;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class QrCodeDecoderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DecodesRotatedAndInvertedQrValuesWithNormalizedBounds(bool inverted)
    {
        const string value = "WIFI:T:WPA;S:fixture;P:private;;";
        var image = Writer().Write(value);
        byte[] rotated = new byte[image.Pixels.Length];
        for (int y = 0; y < 240; y++)
        for (int x = 0; x < 240; x++)
        for (int channel = 0; channel < 4; channel++)
        {
            byte pixel = image.Pixels[(y * 240 + x) * 4 + channel];
            rotated[(x * 240 + 239 - y) * 4 + channel] = inverted && channel < 3 ? (byte)(255 - pixel) : pixel;
        }
        var code = QrCodeDecoder.Decode(rotated, 240, 240).Single();
        Assert.AreEqual(value, code.Value);
        Assert.IsTrue(code.Bounds.X >= 0 && code.Bounds.Y >= 0);
        Assert.IsTrue(code.Bounds.Width > .5 && code.Bounds.X + code.Bounds.Width <= 1);
        Assert.IsTrue(code.Bounds.Height > .5 && code.Bounds.Y + code.Bounds.Height <= 1);
        Assert.IsNull(code.Timestamp);
    }

    [TestMethod]
    public void RetainsTwoLocationsWithTheSameValueAndEmptyImagesAreSuccessful()
    {
        byte[] canvas = Enumerable.Repeat((byte)255, 600 * 280 * 4).ToArray();
        Assert.HasCount(0, QrCodeDecoder.Decode(canvas, 600, 280));
        var image = Writer().Write("https://example.com/fixture");
        foreach (int left in new[] { 10, 330 })
        for (int row = 0; row < 240; row++)
            Array.Copy(image.Pixels, row * 240 * 4, canvas, ((row + 20) * 600 + left) * 4, 240 * 4);
        var results = QrCodeDecoder.Decode(canvas, 600, 280).OrderBy(code => code.Bounds.X).ToArray();
        Assert.HasCount(2, results);
        Assert.AreEqual(results[0].Value, results[1].Value);
        Assert.IsTrue(results[0].Bounds.X < .5 && results[1].Bounds.X > .5);
    }

    [TestMethod]
    public void InvalidBuffersAndCanceledRequestsDoNotScan()
    {
        Assert.ThrowsExactly<ArgumentException>(() => QrCodeDecoder.Decode([], 1, 1));
        Assert.ThrowsExactly<ArgumentException>(() => QrCodeDecoder.Decode([], 0, 0));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => QrCodeDecoder.Decode([], 1, 1, canceled.Token));
    }

    private static BarcodeWriterPixelData Writer() => new()
    {
        Format = BarcodeFormat.QR_CODE,
        Options = new EncodingOptions { Width = 240, Height = 240, Margin = 4 }
    };
}
