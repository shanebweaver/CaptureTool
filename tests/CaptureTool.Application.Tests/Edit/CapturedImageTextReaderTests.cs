using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using Moq;
using System.Drawing;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class CapturedImageTextReaderTests
{
    private static readonly SourceRevision Revision = new(new string('a', 64));
    private static readonly SourceRevision Changed = new(new string('b', 64));
    private static readonly string PathName = Path.GetFullPath("capture.png");
    private readonly Mock<ICaptureAssetCatalog> _catalog = new();
    private readonly Mock<ICaptureMetadataReader> _metadata = new();
    private readonly Mock<IAnalysisSource> _files = new();
    private readonly Mock<IAnalysisSourceLease> _lease = new();
    private readonly CaptureAsset _asset = new(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow,
        PathName, CaptureSourceOwnership.Application);
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestInitialize]
    public void Initialize()
    {
        _catalog.Setup(x => x.ReadAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_asset]);
        _files.Setup(x => x.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_lease.Object);
        _lease.SetupGet(x => x.Revision).Returns(Revision);
        _lease.Setup(x => x.VerifyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetRecord([new("world", new(.4, .2, .2, .1)), new("Hello", new(.1, .2, .2, .1))]);
    }

    [TestMethod]
    public async Task LooksUpFreshMetadataAndMapsWordBoxesToEditorPixelsInReadingOrder()
    {
        var reader = Reader();
        var source = await reader.OpenAsync(new(PathName), new(200, 100), Ct);
        _catalog.Verify(x => x.ReadAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        var document = await reader.ReadAsync(source!, Ct);
        Assert.IsNotNull(document);
        Assert.AreEqual("Hello world", document.Text);
        Assert.IsFalse(document.HasQrCodeResults, "Older OCR metadata must still allow QR detection.");
        Assert.AreEqual(new RectangleF(80, 20, 40, 10), document.Regions[0].Bounds);
        _metadata.Verify(x => x.GetAsync(_asset.Id, Revision, Ct), Times.Once);
        _lease.Verify(x => x.VerifyAsync(Ct), Times.Once);
    }

    [TestMethod]
    public async Task PersistentAliasAllowsAnUnmodifiedWorkingCopy()
    {
        var reader = Reader();
        var image = new ImageFile(Path.GetFullPath("working.png")) { PersistentFilePath = PathName };
        Assert.IsNotNull(await reader.ReadAsync((await reader.OpenAsync(image, new(200, 100), Ct))!, Ct));
    }

    [TestMethod]
    public async Task EmptyOcrIsAReusableSuccessAndUnpositionedTextRemainsAvailable()
    {
        SetRecord([]);
        var source = new CapturedImageTextSource(PathName, null, Revision, new(200, 100));
        var empty = await Reader().ReadAsync(source, Ct);
        Assert.IsNotNull(empty);
        Assert.AreEqual("", empty.Text);
        Assert.HasCount(0, empty.Regions);
        SetRecord([new("Unpositioned"), new("text")]);
        Assert.AreEqual("Unpositioned text", (await Reader().ReadAsync(source, Ct))!.Text);
    }

    [TestMethod]
    [DataRow("changed-file")]
    [DataRow("stale-record")]
    [DataRow("missing")]
    [DataRow("ambiguous")]
    [DataRow("changed-during-read")]
    [DataRow("unavailable")]
    [DataRow("no-record")]
    public async Task UnusableMetadataFallsBackInsteadOfReturningIncorrectText(string reason)
    {
        switch (reason)
        {
            case "changed-file": _lease.SetupGet(x => x.Revision).Returns(Changed); break;
            case "stale-record": SetRecord([], Changed); break;
            case "missing": _catalog.Setup(x => x.ReadAllAsync(Ct)).ReturnsAsync([]); break;
            case "ambiguous": _catalog.Setup(x => x.ReadAllAsync(Ct)).ReturnsAsync([_asset, _asset]); break;
            case "changed-during-read": _lease.Setup(x => x.VerifyAsync(Ct)).ReturnsAsync(false); break;
            case "unavailable": _metadata.Setup(x => x.GetAsync(_asset.Id, Revision, Ct)).ThrowsAsync(new IOException()); break;
            case "no-record": _metadata.Setup(x => x.GetAsync(_asset.Id, Revision, Ct)).ReturnsAsync((CaptureAnalysisRecord?)null); break;
        }
        Assert.IsNull(await Reader().ReadAsync(new(PathName, null, Revision, new(200, 100)), Ct));
    }

    [TestMethod]
    public async Task UnavailableFileIsACacheMissButCancellationPropagates()
    {
        var reader = Reader();
        Assert.IsNull(await reader.OpenAsync(new("relative.png"), new(200, 100), Ct));
        Assert.IsNull(await reader.OpenAsync(new(PathName), Size.Empty, Ct));
        _files.Setup(x => x.OpenAsync(PathName, Ct)).ThrowsAsync(new UnauthorizedAccessException());
        Assert.IsNull(await reader.OpenAsync(new(PathName), new(200, 100), Ct));
        _files.Setup(x => x.OpenAsync(PathName, Ct)).ThrowsAsync(new OperationCanceledException(Ct));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => reader.OpenAsync(new(PathName), new(200, 100), Ct));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => reader.ReadAsync(new(PathName, null, Revision, new(200, 100)), Ct));
    }

    private CapturedImageTextReader Reader() => new(_catalog.Object, _metadata.Object, _files.Object);
    private void SetRecord(RecognizedText[] regions, SourceRevision? revision = null, DecodedQrCode[]? codes = null) =>
        _metadata.Setup(x => x.GetAsync(_asset.Id, Revision, It.IsAny<CancellationToken>())).ReturnsAsync(
            new CaptureAnalysisRecord(_asset.Id, AnalysisMediaKind.Image, revision ?? Revision, "v1", Guid.NewGuid(),
                new[] { new AnalysisResult(new TextRecognitionMetadata(regions), new("ocr", "local", "ocr", "v1"), DateTimeOffset.UtcNow, "v1") }
                    .Concat(codes == null ? [] : new[] { new AnalysisResult(new QrCodeMetadata(codes), new("qr", "local", "qr", "v1"), DateTimeOffset.UtcNow, "v1") })));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SavedQrResultsMapToPixelsAndEmptyScanRemainsDistinctFromMissingData(bool empty)
    {
        SetRecord([], codes: empty ? [] : [new("https://example.com", new(.1, .2, .3, .4))]);
        var document = await Reader().ReadAsync(new(PathName, null, Revision, new(200, 100)), Ct);
        Assert.IsTrue(document!.HasQrCodeResults);
        Assert.HasCount(empty ? 0 : 1, document.QrCodes);
        if (!empty) Assert.AreEqual(new RectangleF(20, 20, 60, 40), document.QrCodes.Single().Bounds);
    }
}
