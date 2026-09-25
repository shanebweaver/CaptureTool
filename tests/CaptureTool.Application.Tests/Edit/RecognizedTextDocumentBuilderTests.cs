using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using System.Drawing;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class RecognizedTextDocumentBuilderTests
{
    [TestMethod]
    public void ReadingOrderQrExclusionAndCopyTextAreConsistent()
    {
        var document = new RecognizedTextDocumentBuilder().Build(new(300, 200),
            [new("world", new(80, 10, 40, 10)), new("Hello", new(10, 10, 40, 10)),
             new("next line", new(10, 30, 70, 10)), new("noise", new(220, 100, 20, 10))],
            [new("decoded", new(200, 80, 80, 80))]);
        Assert.AreEqual(string.Join(Environment.NewLine, "Hello world", "next line", "decoded"), document.Text);
        Assert.HasCount(3, document.Regions);
        Assert.IsTrue(document.HasTextResults);
        Assert.IsTrue(document.HasQrCodeResults);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void MissingAndCompletedEmptyScansRemainIndependent(bool ocr, bool qr)
    {
        var document = new RecognizedTextDocumentBuilder().Build(new(100, 50), ocr ? [] : null, qr ? [] : null);
        Assert.AreEqual(ocr, document.HasTextResults);
        Assert.AreEqual(qr, document.HasQrCodeResults);
        Assert.AreEqual("", document.Text);
    }
}
