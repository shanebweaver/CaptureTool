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
    public void OcrContracts_ShouldEnforceEveryBoundedStructuralLimit()
    {
        var validBounds = new PixelRect(0, 0, 1, 1);
        var validWord = new OcrWordV1("word", validBounds);
        var validLine = new OcrLineV1("line", validBounds, [validWord]);
        var validRegion = new OcrRegionV1(validBounds, [validLine]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PixelRect(-1, 0, 1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 0, 1));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrLanguageCandidateV1(
            new string('a', 65)));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrWordV1(" ", validBounds));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrWordV1("word", default));
        Assert.ThrowsExactly<ArgumentNullException>(() => new OcrLineV1(
            null!,
            validBounds,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrLineV1(
            new string('x', OcrLineV1.MaximumTextLength + 1),
            validBounds,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrLineV1(
            "line",
            default,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrLineV1(
            "line",
            validBounds,
            Enumerable.Repeat(validWord, OcrLineV1.MaximumWordCount + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrRegionV1(
            default,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrRegionV1(
            validBounds,
            Enumerable.Repeat(validLine, OcrRegionV1.MaximumLineCount + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            default,
            string.Empty,
            [],
            []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            null!,
            [],
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            string.Empty,
            Enumerable.Repeat(new OcrLanguageCandidateV1("en"),
                OcrDocumentV1.MaximumLanguageCount + 1),
            []));
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            string.Empty,
            [],
            Enumerable.Repeat(validRegion, OcrDocumentV1.MaximumRegionCount + 1)));
        var invalidLineRegion = new OcrRegionV1(
            new PixelRect(0, 0, 10, 10),
            [new OcrLineV1("outside", new PixelRect(9, 9, 2, 2), [])]);
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            string.Empty,
            [],
            [invalidLineRegion]));
        var invalidWordRegion = new OcrRegionV1(
            new PixelRect(0, 0, 10, 10),
            [new OcrLineV1(
                "outside",
                new PixelRect(0, 0, 10, 10),
                [new OcrWordV1("outside", new PixelRect(9, 9, 2, 2))])]);
        Assert.ThrowsExactly<ArgumentException>(() => new OcrDocumentV1(
            new PixelSize(10, 10),
            string.Empty,
            [],
            [invalidWordRegion]));
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

    [TestMethod]
    public void SpeechTranscriptV1_ShouldNormalizeTextAndPreserveOptionalTimedSegments()
    {
        var segments = new List<SpeechTranscriptSegmentV1>
        {
            new(
                "  discuss the launch plan  ",
                TimeSpan.FromSeconds(12),
                TimeSpan.FromSeconds(15),
                "Speaker 1",
                .8),
        };

        var payload = new SpeechTranscriptV1(
            "  discuss the launch plan\r\nnext step  ",
            segments,
            "en-US");
        segments.Clear();

        Assert.AreEqual(AnalysisCapabilities.SpeechTranscriptV1, payload.Definition);
        Assert.AreEqual(CapabilityResultClassification.MachineExtracted, payload.Definition.Classification);
        Assert.AreEqual("discuss the launch plan\nnext step", payload.FullText);
        Assert.HasCount(1, payload.Segments);
        Assert.AreEqual(TimeSpan.FromSeconds(12), payload.Segments[0].StartTime);
        Assert.AreEqual("Speaker 1", payload.Segments[0].SpeakerLabel);
        Assert.IsTrue(payload.IsEquivalentTo(new SpeechTranscriptV1(
            payload.FullText,
            payload.Segments,
            "en-US")));
    }

    [TestMethod]
    public void SpeechTranscriptV1_ShouldRejectInvalidTimingConfidenceAndBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptSegmentV1(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptSegmentV1(
            "text",
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptSegmentV1(
            "text",
            TimeSpan.Zero,
            endTime: null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SpeechTranscriptSegmentV1(
            "text",
            confidence: 1.1));
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptSegmentV1(
            new string('x', SpeechTranscriptSegmentV1.MaximumTextLength + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptV1(
            new string('x', SpeechTranscriptV1.MaximumFullTextLength + 1)));
        var segment = new SpeechTranscriptSegmentV1("text");
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptV1(
            string.Empty,
            Enumerable.Repeat(segment, SpeechTranscriptV1.MaximumSegmentCount + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new SpeechTranscriptV1(
            string.Empty,
            [null!]));
    }

    [TestMethod]
    public void VideoOcrTrackV1_ShouldNormalizeAndPreserveChronologicalObservations()
    {
        var observations = new List<VideoOcrObservationV1>
        {
            new("  Project cafe\u0301\r\nstatus  ", TimeSpan.Zero, TimeSpan.FromSeconds(2)),
            new("Next screen", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)),
        };

        var payload = new VideoOcrTrackV1(
            "  Project cafe\u0301 status\r\nNext screen  ",
            observations);
        observations.Clear();

        Assert.AreEqual(AnalysisCapabilities.VideoOcrTrackV1, payload.Definition);
        Assert.AreEqual(CapabilityResultClassification.MachineExtracted,
            payload.Definition.Classification);
        Assert.AreEqual("Project caf\u00e9 status\nNext screen", payload.FullText);
        Assert.HasCount(2, payload.Observations);
        Assert.AreEqual("Project caf\u00e9\nstatus", payload.Observations[0].Text);
        Assert.AreEqual(TimeSpan.FromSeconds(2), payload.Observations[0].EndTime);
        Assert.IsTrue(payload.IsEquivalentTo(new VideoOcrTrackV1(
            payload.FullText,
            payload.Observations)));
    }

    [TestMethod]
    public void VideoOcrTrackV1_ShouldRejectInvalidTextTimingOrderingAndBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrObservationV1(
            " ",
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrObservationV1(
            "text",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrObservationV1(
            new string('x', VideoOcrObservationV1.MaximumTextLength + 1),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrTrackV1(
            new string('x', VideoOcrTrackV1.MaximumFullTextLength + 1)));
        var observation = new VideoOcrObservationV1(
            "text",
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrTrackV1(
            string.Empty,
            Enumerable.Repeat(observation, VideoOcrTrackV1.MaximumObservationCount + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrTrackV1(
            string.Empty,
            [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoOcrTrackV1(
            "overlap",
            [
                observation,
                new VideoOcrObservationV1(
                    "overlap",
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(2)),
            ]));
    }

    [TestMethod]
    public void VideoDescriptionTrackV1_ShouldNormalizeAndRemainExplicitlyClassifiedAsInference()
    {
        var observations = new List<VideoDescriptionObservationV1>
        {
            new("  A presenter opens the cafe\u0301 dashboard.  ",
                TimeSpan.Zero,
                TimeSpan.FromSeconds(15)),
            new("A deployment confirmation is visible.",
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)),
        };

        var payload = new VideoDescriptionTrackV1(
            "  A presenter opens the cafe\u0301 dashboard.\r\nA deployment confirmation is visible.  ",
            observations);
        observations.Clear();

        Assert.AreEqual(AnalysisCapabilities.VideoDescriptionTrackV1, payload.Definition);
        Assert.AreEqual(CapabilityResultClassification.Inference,
            payload.Definition.Classification);
        Assert.AreEqual(
            "A presenter opens the caf\u00e9 dashboard.\nA deployment confirmation is visible.",
            payload.FullText);
        Assert.HasCount(2, payload.Observations);
        Assert.AreEqual(TimeSpan.FromSeconds(15), payload.Observations[1].StartTime);
        Assert.IsTrue(payload.IsEquivalentTo(new VideoDescriptionTrackV1(
            payload.FullText,
            payload.Observations)));
    }

    [TestMethod]
    public void VideoDescriptionTrackV1_ShouldRejectInvalidTextTimingOrderingAndBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionObservationV1(
            " ",
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionObservationV1(
            "description",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionObservationV1(
            new string('x', VideoDescriptionObservationV1.MaximumDescriptionLength + 1),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionTrackV1(
            new string('x', VideoDescriptionTrackV1.MaximumFullTextLength + 1)));
        var observation = new VideoDescriptionObservationV1(
            "description",
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionTrackV1(
            string.Empty,
            Enumerable.Repeat(
                observation,
                VideoDescriptionTrackV1.MaximumObservationCount + 1)));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionTrackV1(
            string.Empty,
            [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new VideoDescriptionTrackV1(
            "overlap",
            [
                observation,
                new VideoDescriptionObservationV1(
                    "overlap",
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(2)),
            ]));
    }
}
