using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Reflection;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class CapabilityPayloadTests
{
    [TestMethod]
    public void CapabilityPayload_ShouldBeAClosedCompiledUnion()
    {
        ConstructorInfo[] constructors = typeof(CapabilityPayload).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.HasCount(1, constructors);
        ConstructorInfo constructor = constructors[0];

        Assert.IsTrue(typeof(CapabilityPayload).IsAbstract);
        Assert.IsTrue(constructor.IsFamilyAndAssembly);
        Assert.IsFalse(typeof(CapabilityPayload).IsInterface);
    }

    [TestMethod]
    public void MediaPropertiesV1_ShouldExposeNormalizedObservedFacts()
    {
        var payload = new MediaPropertiesV1(
            CaptureMediaKind.Video,
            new PixelSize(1920, 1080),
            TimeSpan.FromSeconds(2),
            " video/mp4 ",
            "mp4",
            "h264",
            "aac",
            2,
            48000,
            4_000_000,
            60);

        Assert.AreEqual(AnalysisCapabilities.MediaPropertiesV1, payload.Definition);
        Assert.AreEqual("video/mp4", payload.MimeType);
        Assert.AreEqual(4_000_000L, payload.BitRate);
        Assert.IsTrue(payload.IsEquivalentTo(new MediaPropertiesV1(
            CaptureMediaKind.Video,
            new PixelSize(1920, 1080),
            TimeSpan.FromSeconds(2),
            "video/mp4",
            "mp4",
            "h264",
            "aac",
            2,
            48000,
            4_000_000,
            60)));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MediaPropertiesV1(
            CaptureMediaKind.Unknown));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MediaPropertiesV1(
            CaptureMediaKind.Video,
            bitRate: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MediaPropertiesV1(
            CaptureMediaKind.Video,
            frameRate: double.NaN));
    }

    [TestMethod]
    public void OcrDocumentV1_ShouldPreserveOrderedStructureAndDefensivelyCopyCollections()
    {
        var languages = new List<OcrLanguageCandidateV1> { new("en-US", .9) };
        var words = new List<OcrWordV1> { new("hello", new PixelRect(1, 2, 20, 10), .8) };
        var line = new OcrLineV1("hello", new PixelRect(1, 2, 20, 10), words, .75);
        var lines = new List<OcrLineV1> { line };
        var region = new OcrRegionV1(new PixelRect(0, 0, 50, 20), lines, .7);
        var regions = new List<OcrRegionV1> { region };
        var payload = new OcrDocumentV1(new PixelSize(100, 50), "hello", languages, regions);

        languages.Clear();
        words.Clear();
        lines.Clear();
        regions.Clear();

        Assert.AreEqual(AnalysisCapabilities.OcrDocumentV1, payload.Definition);
        Assert.HasCount(1, payload.Languages);
        Assert.HasCount(1, payload.Regions);
        OcrRegionV1 restoredRegion = payload.Regions[0];
        Assert.AreEqual(.7, restoredRegion.Confidence);
        Assert.HasCount(1, restoredRegion.Lines);
        OcrLineV1 restoredLine = restoredRegion.Lines[0];
        Assert.AreEqual(.75, restoredLine.Confidence);
        Assert.HasCount(1, restoredLine.Words);
    }

    [TestMethod]
    public void OcrDocumentV1_ShouldAllowAValidNoTextResult()
    {
        var payload = new OcrDocumentV1(new PixelSize(100, 50), string.Empty, [], []);

        Assert.AreEqual(string.Empty, payload.FullText);
        Assert.IsEmpty(payload.Regions);
    }

    [TestMethod]
    public void OcrContracts_ShouldRejectInvalidConfidenceGeometryAndNullElements()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PixelRect(double.NaN, 0, 1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrLanguageCandidateV1("en", 1.1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrLineV1(
            "line",
            new PixelRect(0, 0, 1, 1),
            [],
            -.1));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            "outside",
            [],
            [new OcrRegionV1(new PixelRect(9, 9, 2, 2), [])]));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrLineV1(
            "line",
            new PixelRect(0, 0, 1, 1),
            [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrRegionV1(
            new PixelRect(0, 0, 1, 1),
            [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            string.Empty,
            [null!],
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrWordV1(
            new string('x', OcrWordV1.MaximumTextLength + 1),
            new PixelRect(0, 0, 1, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            new string('x', OcrDocumentV1.MaximumFullTextLength + 1),
            [],
            []));
    }

    [TestMethod]
    public void ImageDescriptionV1_ShouldRemainExplicitlyClassifiedAsInference()
    {
        var payload = new ImageDescriptionV1(
            "A settings window.",
            ImageDescriptionPurpose.Brief,
            "screenshot",
            .6);

        Assert.AreEqual(AnalysisCapabilities.ImageDescriptionV1, payload.Definition);
        Assert.AreEqual(CapabilityResultClassification.Inference, payload.Definition.Classification);
        Assert.AreEqual(ImageDescriptionPurpose.Brief, payload.Purpose);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ImageDescriptionV1(
            "Description",
            ImageDescriptionPurpose.Unknown));
    }
}
