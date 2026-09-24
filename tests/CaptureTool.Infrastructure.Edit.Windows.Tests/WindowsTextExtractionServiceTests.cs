using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using WinPoint = Windows.Foundation.Point;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class WindowsTextExtractionServiceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CompleteStoredResultsSkipImageDecodingAndQrScanningEvenWhenNoCodesWereFound(bool empty)
    {
        var existing = new RecognizedTextDocument("saved", new(240, 240), [],
            empty ? [] : [new("https://example.com", new(10, 10, 100, 100))]);
        var result = await new WindowsTextExtractionService().ExtractAsync(new(Stream.Null, existing.ImageSize, existing));
        Assert.AreEqual(TextExtractionStatus.Success, result.Status);
        Assert.AreSame(existing, result.Document);
    }

    [TestMethod]
    public async Task ExistingOcrKeepsItsTextAndWordBoxesAndStillDetectsQrCodes()
    {
        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions { Width = 240, Height = 240, Margin = 4 }
        };
        var pixels = writer.Write("https://example.com/capture/42");
        using var encoded = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await global::Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(global::Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, encoded);
        encoder.SetPixelData(global::Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, global::Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
            (uint)pixels.Width, (uint)pixels.Height, 96, 96, pixels.Pixels);
        await encoder.FlushAsync();
        encoded.Seek(0);
        using var source = new MemoryStream();
        using (var encodedStream = encoded.AsStreamForRead()) await encodedStream.CopyToAsync(source);
        source.Position = 0;
        var existing = new RecognizedTextDocument("saved text", new(240, 240), [new("saved text", new(10, 10, 20, 10))]);
        var result = await new WindowsTextExtractionService().ExtractAsync(new(source, new(240, 240), existing));
        Assert.AreEqual(TextExtractionStatus.Success, result.Status);
        Assert.AreEqual(existing.Text, result.Document!.Text);
        Assert.AreEqual(existing.Regions[0], result.Document.Regions[0]);
        Assert.HasCount(1, result.Document.QrCodes);
        Assert.AreEqual("https://example.com/capture/42", result.Document.QrCodes[0].Value);
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
