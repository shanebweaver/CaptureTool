using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain.Analysis;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal readonly record struct CaptureAnalysisDocumentHeader(
    int SchemaVersion,
    long DocumentRevision);

internal static class CaptureAnalysisDocumentSerializer
{
    public static byte[] SerializeControl(
        CaptureAnalysisControlState state,
        long documentRevision,
        int schemaVersion)
    {
        CaptureAnalysisControlDocument document = CaptureAnalysisDocumentMapper.ToDocument(
            state,
            documentRevision,
            schemaVersion);
        return JsonSerializer.SerializeToUtf8Bytes(
            document,
            CaptureAnalysisJsonContext.Default.CaptureAnalysisControlDocument);
    }

    public static CaptureAnalysisControlSnapshot DeserializeControl(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        CaptureAnalysisControlDocument document = JsonSerializer.Deserialize(
            plaintext,
            CaptureAnalysisJsonContext.Default.CaptureAnalysisControlDocument)
            ?? throw new InvalidDataException("The Capture Analysis control document is empty.");
        return CaptureAnalysisDocumentMapper.ToDomain(document);
    }

    public static byte[] SerializeEnvelope(
        CaptureAnalysisRecord record,
        long documentRevision,
        int schemaVersion,
        IEnumerable<JsonElement>? opaqueCapabilityEntries = null)
    {
        CaptureAnalysisEnvelopeDocument document = CaptureAnalysisDocumentMapper.ToDocument(
            record,
            documentRevision,
            schemaVersion,
            opaqueCapabilityEntries);
        return JsonSerializer.SerializeToUtf8Bytes(
            document,
            CaptureAnalysisJsonContext.Default.CaptureAnalysisEnvelopeDocument);
    }

    public static CaptureAnalysisEnvelopeReadResult DeserializeEnvelope(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        CaptureAnalysisEnvelopeDocument document = JsonSerializer.Deserialize(
            plaintext,
            CaptureAnalysisJsonContext.Default.CaptureAnalysisEnvelopeDocument)
            ?? throw new InvalidDataException("The Capture Analysis envelope is empty.");
        return CaptureAnalysisDocumentMapper.ToDomain(document);
    }

    public static CaptureAnalysisDocumentHeader ReadHeader(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        using JsonDocument document = JsonDocument.Parse(plaintext);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out JsonElement schemaVersionElement) ||
            !schemaVersionElement.TryGetInt32(out int schemaVersion) ||
            schemaVersion <= 0 ||
            !root.TryGetProperty("documentRevision", out JsonElement documentRevisionElement) ||
            !documentRevisionElement.TryGetInt64(out long documentRevision) ||
            documentRevision <= 0)
        {
            throw new InvalidDataException(
                "A Capture Analysis document requires positive schema and document revisions.");
        }

        return new(schemaVersion, documentRevision);
    }
}
