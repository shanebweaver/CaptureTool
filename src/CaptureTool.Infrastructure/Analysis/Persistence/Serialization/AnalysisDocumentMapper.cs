using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Security.Cryptography;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal static class AnalysisDocumentMapper
{
    public static AnalysisDocument ToDocument(CaptureAnalysisRecord record) =>
        new(1, record.CaptureId.Value, (int)record.MediaKind, record.SourceRevision.Sha256,
            record.PlanVersion, record.RunId, record.Results.Select(ToDocument).ToArray());

    public static CaptureAnalysisRecord ToRecord(AnalysisDocument document)
    {
        if (document.Version is not (1 or 2) || document.Results == null || document.SourceSha256 == null)
            throw new InvalidDataException("Unsupported or invalid analysis document.");
        try
        {
            return new(new CaptureId(document.CaptureId), (AnalysisMediaKind)document.MediaKind,
                new SourceRevision(document.SourceSha256), document.PlanVersion, document.RunId,
                document.Results.Select(ToResult));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Invalid analysis metadata.", exception);
        }
    }

    private static ResultDocument ToDocument(AnalysisResult result)
    {
        AnalyzerProvenance producer = result.Producer;
        var document = new ResultDocument(result.Payload.Capability.Name, result.Payload.Capability.SchemaVersion,
            new(producer.AnalyzerId, producer.ProviderId, producer.ModelId, producer.AdapterVersion, producer.ModelVersion),
            result.GeneratedAt, result.PlanVersion, null, null, null, result.ProducingRunId,
            ResultId: result.ResultId,
            Inputs: result.Derivation?.Inputs.Select(input => new InputReferenceDocument(
                new(input.Capability.Name, input.Capability.SchemaVersion), input.ResultId)).ToArray());
        return result.Payload switch
        {
            StructuredFactsMetadata facts => document with
            {
                Facts = facts.Facts.Select(fact => new FactDocument((int)fact.Kind, fact.Value,
                    fact.Evidence.Select(evidence => new EvidenceDocument(evidence.ResultId,
                        evidence.EntryIndex, evidence.Start, evidence.Length)).ToArray())).ToArray(),
                Coverage = facts.Coverage is { } coverage ? new(coverage.AvailableEntries, coverage.IncludedEntries,
                    coverage.IncludedCharacters, (int)coverage.Limits) : null,
            },
            FileDetailsMetadata file => document with
            {
                FileDetails = new((int)file.MediaKind, file.FileName, file.SizeBytes, file.ContentType,
                    file.FileCreatedAt, file.FileModifiedAt, file.CapturedAt, file.Duration?.Ticks,
                    file.Image is { } image ? new(new(image.Dimensions.Width, image.Dimensions.Height), image.DpiX, image.DpiY) : null,
                    file.Video is { } video ? new(new(video.Dimensions.Width, video.Dimensions.Height), video.FrameRate, video.Bitrate, video.Codec) : null,
                    file.Audio is { } audio ? new(audio.Channels, audio.SampleRate, audio.Bitrate, audio.Codec) : null,
                    CaptureTimeVerified: file.CapturedAt != null),
            },
            QrCodeMetadata qr => document with
            {
                QrCodes = qr.Codes.Select(code => new QrCodeDocument(code.Value,
                    new(code.Bounds.X, code.Bounds.Y, code.Bounds.Width, code.Bounds.Height), code.Timestamp?.Ticks)).ToArray(),
            },
            TextRecognitionMetadata text => document with
            {
                Text = text.Regions.Select(region => new TextDocument(region.Text,
                    region.Bounds is { } bounds ? new(bounds.X, bounds.Y, bounds.Width, bounds.Height) : null,
                    region.Timestamp?.Ticks)).ToArray(),
            },
            DescriptionMetadata descriptions => document with
            {
                Descriptions = descriptions.Descriptions.Select(description =>
                    new DescriptionDocument(description.Text, description.Timestamp?.Ticks)).ToArray(),
            },
            TranscriptMetadata transcript => document with
            {
                Transcript = new(transcript.Language, transcript.Segments.Select(segment =>
                    new SegmentDocument(segment.Text, segment.Start.Ticks, segment.End.Ticks)).ToArray()),
            },
            _ => throw new InvalidDataException("Unregistered metadata payload type."),
        };
    }

    private static AnalysisResult ToResult(ResultDocument document)
    {
        if (document == null || document.SchemaVersion != 1 || document.Producer == null)
            throw new InvalidDataException("Unsupported or invalid analysis result.");
        if (document.Facts != null && document.ResultId == null)
            throw new InvalidDataException("Derived metadata requires a persisted result identity.");
        if (document.Facts == null && document.Coverage != null)
            throw new InvalidDataException("Processing coverage belongs to structured facts metadata.");
        AnalysisPayload payload = document switch
        {
            { Capability: "structured-facts", Facts: not null, FileDetails: null, Text: null, Descriptions: null, Transcript: null, QrCodes: null } =>
                new StructuredFactsMetadata(document.Facts.Select(ToFact), document.Coverage is { } coverage ?
                    new(coverage.AvailableEntries, coverage.IncludedEntries, coverage.IncludedCharacters, (MetadataProcessingLimit)coverage.Limits) : null),
            { Capability: "file-details", FileDetails: not null, Text: null, Descriptions: null, Transcript: null, QrCodes: null, Facts: null } =>
                ToFileDetails(document.FileDetails),
            { Capability: "qr-code-detection", QrCodes: not null, Text: null, Descriptions: null, Transcript: null, FileDetails: null, Facts: null } =>
                new QrCodeMetadata(document.QrCodes.Select(ToQrCode)),
            { Capability: "text-recognition", Text: not null, Descriptions: null, Transcript: null, QrCodes: null, FileDetails: null, Facts: null } =>
                new TextRecognitionMetadata(document.Text.Select(ToText)),
            { Capability: "description", Descriptions: not null, Text: null, Transcript: null, QrCodes: null, FileDetails: null, Facts: null } =>
                new DescriptionMetadata(document.Descriptions.Select(ToDescription)),
            { Capability: "transcription", Transcript.Segments: not null, Text: null, Descriptions: null, QrCodes: null, FileDetails: null, Facts: null } =>
                new TranscriptMetadata(document.Transcript.Language, document.Transcript.Segments.Select(ToSegment)),
            _ => throw new InvalidDataException("Unsupported or ambiguous metadata payload."),
        };
        ProducerDocument producer = document.Producer;
        return new(payload, new(producer.AnalyzerId, producer.ProviderId, producer.ModelId,
            producer.AdapterVersion, producer.ModelVersion), document.GeneratedAt, document.PlanVersion, document.ProducingRunId,
            document.ResultId ?? LegacyResultId(document),
            document.Inputs == null ? null : new AnalysisDerivation(document.Inputs.Select(ToInputReference)));
    }

    // Older documents have no identity. Hash their canonical source-generated representation so
    // repeated reads do not change it; the next ordinary write persists the assigned identity.
    private static Guid LegacyResultId(ResultDocument document) =>
        new(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document, AnalysisJsonContext.Default.ResultDocument)).AsSpan(0, 16));

    private static AnalysisInputReference ToInputReference(InputReferenceDocument document)
    {
        if (document?.Capability == null) throw new InvalidDataException("Missing derived input reference.");
        return new(new(document.Capability.Name, document.Capability.SchemaVersion), document.ResultId);
    }

    private static StructuredFact ToFact(FactDocument document)
    {
        if (document?.Evidence == null) throw new InvalidDataException("Missing fact evidence.");
        return new((StructuredFactKind)document.Kind, document.Value, document.Evidence.Select(evidence =>
            evidence == null ? throw new InvalidDataException("Missing evidence span.") :
                new AnalysisEvidence(evidence.ResultId, evidence.EntryIndex, evidence.Start, evidence.Length)));
    }

    private static RecognizedText ToText(TextDocument document)
    {
        if (document == null) throw new InvalidDataException("Missing text region.");
        BoundsDocument? bounds = document.Bounds;
        return new(document.Text, bounds == null ? null : new(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
    }

    private static FileDetailsMetadata ToFileDetails(FileDetailsDocument file) =>
        new((AnalysisMediaKind)file.MediaKind, file.FileName, file.SizeBytes, file.ContentType,
            // Older file-details results could confuse historical activity with capture time.
            // Keep all other facts, but expose unverified capture dates as unknown until reanalysis.
            file.FileCreatedAt, file.FileModifiedAt, file.CaptureTimeVerified ? file.CapturedAt : null,
            file.DurationTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null,
            file.Image is { } image ? new(ToDimensions(image.Dimensions), image.DpiX, image.DpiY) : null,
            file.Video is { } video ? new(ToDimensions(video.Dimensions), video.FrameRate, video.Bitrate, video.Codec) : null,
            file.Audio is { } audio ? new(audio.Channels, audio.SampleRate, audio.Bitrate, audio.Codec) : null);

    private static MediaDimensions ToDimensions(DimensionsDocument dimensions) => dimensions == null
        ? throw new InvalidDataException("Missing media dimensions.") : new(dimensions.Width, dimensions.Height);

    private static DecodedQrCode ToQrCode(QrCodeDocument document)
    {
        if (document?.Bounds == null) throw new InvalidDataException("Missing QR code bounds.");
        BoundsDocument bounds = document.Bounds;
        return new(document.Value, new(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
    }

    private static MediaDescription ToDescription(DescriptionDocument document)
    {
        if (document == null) throw new InvalidDataException("Missing description.");
        return new(document.Text, document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
    }

    private static TranscriptSegment ToSegment(SegmentDocument document)
    {
        if (document == null) throw new InvalidDataException("Missing transcript segment.");
        return new(document.Text, TimeSpan.FromTicks(document.StartTicks), TimeSpan.FromTicks(document.EndTicks));
    }
}
