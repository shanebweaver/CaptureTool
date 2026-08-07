using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Infrastructure.Edit.Windows;
using FluentAssertions;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class QrCodeDetectorTests
{
    [TestMethod]
    public void Detect_WhenImageContainsQrCode_ReturnsPayloadAndBounds()
    {
        const string payload = "https://example.com/capture/42";
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = 240,
                Height = 240,
                Margin = 4
            }
        };
        PixelData image = writer.Write(payload);

        var result = QrCodeDetector.Detect(image.Pixels, image.Width, image.Height);

        result.Should().ContainSingle();
        result[0].Value.Should().Be(payload);
        result[0].Bounds.Width.Should().BeGreaterThan(150);
        result[0].Bounds.Height.Should().BeGreaterThan(150);
    }

    [TestMethod]
    public void Detect_WhenImageHasNoQrCode_ReturnsEmptyResult()
    {
        byte[] whitePixels = Enumerable.Repeat((byte)255, 100 * 100 * 4).ToArray();

        QrCodeDetector.Detect(whitePixels, 100, 100).Should().BeEmpty();
    }

    [TestMethod]
    public void ShouldExcludeText_WhenOcrRegionIsInsideQrCode_ReturnsTrue()
    {
        RecognizedQrCodeRegion[] qrCodes =
        [
            new("https://example.com", new System.Drawing.RectangleF(40, 40, 120, 120))
        ];

        QrCodeDetector.ShouldExcludeText(
            new System.Drawing.RectangleF(70, 85, 28, 14),
            qrCodes).Should().BeTrue();
        QrCodeDetector.ShouldExcludeText(
            new System.Drawing.RectangleF(10, 10, 24, 12),
            qrCodes).Should().BeFalse();
    }
}
