using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using WinPoint = Windows.Foundation.Point;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsTextExtractionServiceTests
{
    [TestMethod]
    public async Task StandaloneExtractionReadsCurrentImageTextAndQrWithoutMetadata()
    {
        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions { Width = 240, Height = 240, Margin = 4 }
        };
        var pixels = writer.Write("https://example.com/capture/42");
        using var image = new Bitmap(800, 300);
        using (var graphics = Graphics.FromImage(image))
        using (var font = new Font("Arial", 30))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("CAPTURE TOOL", font, Brushes.Black, 15, 80);
            for (int y = 0; y < pixels.Height; y++)
                for (int x = 0; x < pixels.Width; x++)
                    image.SetPixel(x + 530, y + 20, pixels.Pixels[(y * pixels.Width + x) * 4] == 0 ? Color.Black : Color.White);
        }
        using var source = new MemoryStream();
        image.Save(source, System.Drawing.Imaging.ImageFormat.Png);
        source.Position = 0;
        var service = new WindowsTextExtractionService(new RecognizedTextDocumentBuilder());
        var result = await service.ExtractAsync(new(source, new(800, 300)));
        Assert.AreEqual(TextExtractionStatus.Success, result.Status);
        Assert.IsNotNull(result.Document);
        Assert.HasCount(1, result.Document.QrCodes);
        Assert.AreEqual("https://example.com/capture/42", result.Document.QrCodes[0].Value);
        if (global::Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() != null)
            StringAssert.Contains(result.Document.Text, "CAPTURE");
    }

    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, false, TextExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, false, TextExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotReady, true, TextExtractionReadyState.PreparationNeeded)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, false, TextExtractionReadyState.NotSupported)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, true, TextExtractionReadyState.Ready)]
    [DataRow(AIFeatureReadyState.DisabledByUser, false, TextExtractionReadyState.Disabled)]
    [DataRow(AIFeatureReadyState.DisabledByUser, true, TextExtractionReadyState.Ready)]
    public void GetCombinedReadyState_ReturnsExpectedState(
        AIFeatureReadyState source,
        bool isLegacyOcrAvailable,
        TextExtractionReadyState expected)
    {
        Assert.AreEqual(
            expected,
            WindowsTextExtractionService.GetCombinedReadyState(source, isLegacyOcrAvailable));
    }

    [TestMethod]
    public void ToRectangleF_ReturnsAxisAlignedBoundsForRotatedText()
    {
        var bounds = new RecognizedTextBoundingBox
        {
            TopLeft = new WinPoint(20, 10),
            TopRight = new WinPoint(80, 20),
            BottomRight = new WinPoint(70, 50),
            BottomLeft = new WinPoint(10, 40)
        };

        RectangleF result = WindowsTextExtractionService.ToRectangleF(bounds);

        Assert.AreEqual(RectangleF.FromLTRB(10, 10, 80, 50), result);
    }
}
