using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

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
            result.GeneratedAt, result.PlanVersion, null, null, null, result.ProducingRunId);
        return result.Payload switch
        {
            FileDetailsMetadata file => document with
            {
                FileDetails = new((int)file.MediaKind, file.FileName, file.SizeBytes, file.ContentType,
                    file.FileCreatedAt, file.FileModifiedAt, file.CapturedAt, file.Duration?.Ticks,
                    file.Image is { } image ? new(new(image.Dimensions.Width, image.Dimensions.Height), image.DpiX, image.DpiY) : null,
                    file.Video is { } video ? new(new(video.Dimensions.Width, video.Dimensions.Height), video.FrameRate, video.Bitrate, video.Codec) : null,
                    file.Audio is { } audio ? new(audio.Channels, audio.SampleRate, audio.Bitrate, audio.Codec) : null),
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
        AnalysisPayload payload = document switch
        {
            { Capability: "file-details", FileDetails: not null, Text: null, Descriptions: null, Transcript: null, QrCodes: null } =>
                ToFileDetails(document.FileDetails),
            { Capability: "qr-code-detection", QrCodes: not null, Text: null, Descriptions: null, Transcript: null, FileDetails: null } =>
                new QrCodeMetadata(document.QrCodes.Select(ToQrCode)),
            { Capability: "text-recognition", Text: not null, Descriptions: null, Transcript: null, QrCodes: null, FileDetails: null } =>
                new TextRecognitionMetadata(document.Text.Select(ToText)),
            { Capability: "description", Descriptions: not null, Text: null, Transcript: null, QrCodes: null, FileDetails: null } =>
                new DescriptionMetadata(document.Descriptions.Select(ToDescription)),
            { Capability: "transcription", Transcript.Segments: not null, Text: null, Descriptions: null, QrCodes: null, FileDetails: null } =>
                new TranscriptMetadata(document.Transcript.Language, document.Transcript.Segments.Select(ToSegment)),
            _ => throw new InvalidDataException("Unsupported or ambiguous metadata payload."),
        };
        ProducerDocument producer = document.Producer;
        return new(payload, new(producer.AnalyzerId, producer.ProviderId, producer.ModelId,
            producer.AdapterVersion, producer.ModelVersion), document.GeneratedAt, document.PlanVersion, document.ProducingRunId);
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
            file.FileCreatedAt, file.FileModifiedAt, file.CapturedAt,
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
