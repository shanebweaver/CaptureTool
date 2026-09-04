using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using System.Text.Json.Nodes;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis.Persistence;

[TestClass]
public sealed class CaptureAnalysisSerializationGoldenTests
{
    [TestMethod]
    public void GoldenEnvelope_ShouldPreserveNumericTemporalGeometryAndStructuredPayloadTypes()
    {
        byte[] goldenBytes = File.ReadAllBytes(GetGoldenFilePath());
        CaptureAnalysisDocumentHeader header = CaptureAnalysisDocumentSerializer.ReadHeader(goldenBytes);
        CaptureAnalysisEnvelopeReadResult loaded =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(goldenBytes);
        CaptureAnalysisRecord record = loaded.Snapshot.Record;

        Assert.AreEqual(LocalCaptureAnalysisStore.CurrentSchemaVersion, header.SchemaVersion);
        Assert.AreEqual(42, header.DocumentRevision);
        Assert.AreEqual(42, loaded.Snapshot.DocumentRevision);
        Assert.AreEqual(9_876_543_210, record.SourceRevision.Length);
        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 7, 6, 16, 30, 123, TimeSpan.Zero),
            record.SourceRevision.LastWriteTimeUtc);

        MediaPropertiesV1 media = GetPayload<MediaPropertiesV1>(
            record,
            AnalysisCapabilities.MediaPropertiesV1);
        Assert.AreEqual(new PixelSize(1920, 1080), media.PixelSize);
        Assert.AreEqual(TimeSpan.FromTicks(12_345_678), media.Duration);
        Assert.AreEqual(18_000_000_001, media.BitRate);
        Assert.AreEqual(59.940_059_940_1, media.FrameRate);

        OcrDocumentV1 ocr = GetPayload<OcrDocumentV1>(
            record,
            AnalysisCapabilities.OcrDocumentV1);
        Assert.AreEqual("OCR-CANARY-海-12345", ocr.FullText);
        Assert.AreEqual(new PixelRect(10.25, 20.5, 600.75, 140.125), ocr.Regions[0].Bounds);
        Assert.AreEqual(
            new PixelRect(14.75, 28.5, 240.125, 42.25),
            ocr.Regions[0].Lines[0].Words[0].Bounds);
        Assert.AreEqual(0.876_543_21, ocr.Regions[0].Lines[0].Words[0].Confidence);

        ImageDescriptionV1 description = GetPayload<ImageDescriptionV1>(
            record,
            AnalysisCapabilities.ImageDescriptionV1);
        Assert.AreEqual(ImageDescriptionPurpose.Brief, description.Purpose);
        Assert.AreEqual("technical-ui", description.Style);

        byte[] rewritten = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            record,
            header.DocumentRevision,
            header.SchemaVersion,
            loaded.OpaqueCapabilityEntries);
        Assert.IsTrue(JsonNode.DeepEquals(
            JsonNode.Parse(goldenBytes),
            JsonNode.Parse(rewritten)));
    }

    [TestMethod]
    public void SerializerContext_ShouldExposeGeneratedMetadataForEveryPersistenceRoot()
    {
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.CaptureAnalysisControlDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.CaptureAnalysisEnvelopeDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.CaptureAnalysisCapabilityEntryDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.MediaPropertiesPayloadDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.OcrDocumentPayloadDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.ImageDescriptionPayloadDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.SpeechTranscriptPayloadDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.VideoOcrTrackPayloadDocument);
        Assert.IsNotNull(CaptureAnalysisJsonContext.Default.VideoDescriptionTrackPayloadDocument);
    }

    [TestMethod]
    public void SpeechTranscriptEnvelope_ShouldRoundTripTimedSegmentsAndProvenance()
    {
        CaptureId captureId = CaptureId.New();
        var recipe = new CaptureAnalysisRecipe(
            new AnalysisRecipeId("capture-memory-audio"),
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Audio,
            [new RecipeCapability(
                AnalysisCapabilities.SpeechTranscriptV1,
                RecipeCapabilityRequirement.Required)]);
        var transcript = new SpeechTranscriptV1(
            "Deploy the audio pipeline tomorrow.",
            [new SpeechTranscriptSegmentV1(
                "Deploy the audio pipeline",
                TimeSpan.FromSeconds(12.25),
                TimeSpan.FromSeconds(14.75),
                "speaker-1",
                0.875)],
            "en-US");
        var result = new CanonicalCapabilityResult(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            transcript,
            AnalysisPersistenceTestData.Analyzer,
            ProcessingBoundary.OnDevice,
            AnalysisPersistenceTestData.CapturedAtUtc.AddSeconds(1));
        var record = new CaptureAnalysisRecord(
            captureId,
            CaptureMediaKind.Audio,
            AnalysisPersistenceTestData.CapturedAtUtc,
            AnalysisPersistenceTestData.SourceRevision,
            recipe,
            [new CapabilityAnalysis(AnalysisCapabilities.SpeechTranscriptV1, result, null)]);

        byte[] bytes = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            record,
            documentRevision: 3,
            LocalCaptureAnalysisStore.CurrentSchemaVersion);
        CaptureAnalysisEnvelopeReadResult restored =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(bytes);

        AnalysisPersistenceTestData.AssertRecordsEquivalent(record, restored.Snapshot.Record);
        SpeechTranscriptV1 payload = GetPayload<SpeechTranscriptV1>(
            restored.Snapshot.Record,
            AnalysisCapabilities.SpeechTranscriptV1);
        Assert.AreEqual("en-US", payload.LanguageTag);
        Assert.AreEqual(TimeSpan.FromSeconds(12.25), payload.Segments[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(14.75), payload.Segments[0].EndTime);
        Assert.AreEqual("speaker-1", payload.Segments[0].SpeakerLabel);
        Assert.AreEqual(0.875, payload.Segments[0].Confidence);
    }

    [TestMethod]
    public void VideoOcrEnvelope_ShouldRoundTripTimedObservationsAndProvenance()
    {
        CaptureId captureId = CaptureId.New();
        CaptureAnalysisRecipe recipe =
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryVideoRecipe();
        var track = new VideoOcrTrackV1(
            "Build status\nReady to deploy",
            [
                new VideoOcrObservationV1(
                    "Build status",
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(4.5)),
                new VideoOcrObservationV1(
                    "Ready to deploy",
                    TimeSpan.FromSeconds(8.25),
                    TimeSpan.FromSeconds(9)),
            ]);
        var result = new CanonicalCapabilityResult(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            track,
            AnalysisPersistenceTestData.Analyzer,
            ProcessingBoundary.OnDevice,
            AnalysisPersistenceTestData.CapturedAtUtc.AddSeconds(1));
        var record = new CaptureAnalysisRecord(
            captureId,
            CaptureMediaKind.Video,
            AnalysisPersistenceTestData.CapturedAtUtc,
            AnalysisPersistenceTestData.SourceRevision,
            recipe,
            [new CapabilityAnalysis(AnalysisCapabilities.VideoOcrTrackV1, result, null)]);

        byte[] bytes = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            record,
            documentRevision: 4,
            LocalCaptureAnalysisStore.CurrentSchemaVersion);
        CaptureAnalysisEnvelopeReadResult restored =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(bytes);

        AnalysisPersistenceTestData.AssertRecordsEquivalent(record, restored.Snapshot.Record);
        VideoOcrTrackV1 payload = GetPayload<VideoOcrTrackV1>(
            restored.Snapshot.Record,
            AnalysisCapabilities.VideoOcrTrackV1);
        Assert.AreEqual(TimeSpan.FromSeconds(2), payload.Observations[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(4.5), payload.Observations[0].EndTime);
        Assert.AreEqual("Ready to deploy", payload.Observations[1].Text);
    }

    [TestMethod]
    public void VideoDescriptionEnvelope_ShouldRoundTripTimedInferenceAndProvenance()
    {
        CaptureId captureId = CaptureId.New();
        CaptureAnalysisRecipe recipe =
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryVideoRecipe();
        var track = new VideoDescriptionTrackV1(
            "A dashboard is visible.\nThe deployment completes.",
            [
                new VideoDescriptionObservationV1(
                    "A dashboard is visible.",
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(15)),
                new VideoDescriptionObservationV1(
                    "The deployment completes.",
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(30)),
            ]);
        var result = new CanonicalCapabilityResult(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            track,
            AnalysisPersistenceTestData.Analyzer,
            ProcessingBoundary.OnDevice,
            AnalysisPersistenceTestData.CapturedAtUtc.AddSeconds(1));
        var record = new CaptureAnalysisRecord(
            captureId,
            CaptureMediaKind.Video,
            AnalysisPersistenceTestData.CapturedAtUtc,
            AnalysisPersistenceTestData.SourceRevision,
            recipe,
            [new CapabilityAnalysis(
                AnalysisCapabilities.VideoDescriptionTrackV1,
                result,
                null)]);

        byte[] bytes = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            record,
            documentRevision: 5,
            LocalCaptureAnalysisStore.CurrentSchemaVersion);
        CaptureAnalysisEnvelopeReadResult restored =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(bytes);

        AnalysisPersistenceTestData.AssertRecordsEquivalent(record, restored.Snapshot.Record);
        VideoDescriptionTrackV1 payload = GetPayload<VideoDescriptionTrackV1>(
            restored.Snapshot.Record,
            AnalysisCapabilities.VideoDescriptionTrackV1);
        Assert.AreEqual(TimeSpan.Zero, payload.Observations[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(30), payload.Observations[1].EndTime);
        Assert.AreEqual("The deployment completes.", payload.Observations[1].Description);
    }

    [TestMethod]
    public void LegacyControlDocumentWithoutChangeCheckpoint_ShouldDefaultToBeginningOfFeed()
    {
        byte[] current = CaptureAnalysisDocumentSerializer.SerializeControl(
            new CaptureAnalysisControlState(CaptureAnalysisPolicy.Unknown, []),
            documentRevision: 1,
            LocalCaptureAnalysisControlStore.CurrentSchemaVersion);
        JsonObject document = JsonNode.Parse(current)!.AsObject();
        Assert.IsTrue(document.Remove("captureChangeCheckpoint"));

        CaptureAnalysisControlSnapshot restored = CaptureAnalysisDocumentSerializer
            .DeserializeControl(Encoding.UTF8.GetBytes(document.ToJsonString()));

        Assert.AreEqual(0, restored.State.CaptureChangeCheckpoint);
    }

    [TestMethod]
    public void LegacyEnvelopeWithoutResultIds_ShouldDeriveStableIdsAndPersistThemOnRewrite()
    {
        CaptureAnalysisRecord record = AnalysisPersistenceTestData.CreateRecord();
        byte[] current = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            record,
            documentRevision: 7,
            LocalCaptureAnalysisStore.CurrentSchemaVersion);
        JsonObject legacy = JsonNode.Parse(current)!.AsObject();
        foreach (JsonNode? entry in legacy["capabilityEntries"]!.AsArray())
        {
            Assert.IsTrue(entry!["canonicalResult"]!.AsObject().Remove("resultId"));
        }

        byte[] legacyBytes = Encoding.UTF8.GetBytes(legacy.ToJsonString());
        CaptureAnalysisEnvelopeReadResult first =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(legacyBytes);
        CaptureAnalysisEnvelopeReadResult second =
            CaptureAnalysisDocumentSerializer.DeserializeEnvelope(legacyBytes);
        CapabilityResultId[] firstIds = first.Snapshot.Record.Analyses
            .OrderBy(analysis => analysis.Capability.Id.Value, StringComparer.Ordinal)
            .Select(analysis => analysis.CanonicalResult!.ResultId)
            .ToArray();
        CapabilityResultId[] secondIds = second.Snapshot.Record.Analyses
            .OrderBy(analysis => analysis.Capability.Id.Value, StringComparer.Ordinal)
            .Select(analysis => analysis.CanonicalResult!.ResultId)
            .ToArray();

        CollectionAssert.AreEqual(firstIds, secondIds);
        Assert.IsTrue(firstIds.All(id => !id.IsEmpty));

        byte[] rewritten = CaptureAnalysisDocumentSerializer.SerializeEnvelope(
            first.Snapshot.Record,
            first.Snapshot.DocumentRevision,
            LocalCaptureAnalysisStore.CurrentSchemaVersion,
            first.OpaqueCapabilityEntries);
        JsonArray rewrittenEntries = JsonNode.Parse(rewritten)!["capabilityEntries"]!.AsArray();
        Assert.IsTrue(rewrittenEntries.All(entry =>
            entry!["canonicalResult"]!["resultId"] != null));
    }

    private static TPayload GetPayload<TPayload>(
        CaptureAnalysisRecord record,
        CapabilityDefinition capability)
        where TPayload : CapabilityPayload
    {
        Assert.IsTrue(record.TryGetAnalysis(capability.Id, out CapabilityAnalysis? analysis));
        return (TPayload)analysis!.CanonicalResult!.Payload;
    }

    private static string GetGoldenFilePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Analysis",
            "Persistence",
            "GoldenFiles",
            "capture-analysis-envelope-v1.json");
    }
}
