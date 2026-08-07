using CaptureTool.Application.Abstractions.Analysis.Persistence;
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
