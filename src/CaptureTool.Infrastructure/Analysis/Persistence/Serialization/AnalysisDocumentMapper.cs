using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal static class AnalysisDocumentMapper
{
    public static AnalysisDocument ToDocument(CaptureAnalysisRecord record) =>
        new(Version: 2,
            CaptureId: record.CaptureId.Value,
            MediaKind: (int)record.MediaKind,
            SourceSha256: record.SourceRevision.Sha256,
            PlanVersion: record.PlanVersion,
            RunId: record.RunId,
            Results: record.Results.Select(ToDocument).ToArray());

    public static CaptureAnalysisRecord ToRecord(AnalysisDocument document)
    {
        if (document.Version != 2 || document.Results == null || document.SourceSha256 == null)
        {
            throw new InvalidDataException("Unsupported or invalid analysis document.");
        }

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
        var document = new ResultDocument(
            Capability: result.Payload.Capability.Name,
            SchemaVersion: result.Payload.Capability.SchemaVersion,
            Producer: ToDocument(result.Producer),
            GeneratedAt: result.GeneratedAt,
            PlanVersion: result.PlanVersion,
            Text: null,
            Descriptions: null,
            Transcript: null,
            ProducingRunId: result.ProducingRunId,
            ResultId: result.ResultId,
            Inputs: result.Derivation?.Inputs.Select(ToDocument).ToArray());
        return result.Payload switch
        {
            CaptureNameMetadata name => document with
            {
                Name = new(name.Suggestion == null ? null : ToDocument(name.Suggestion)),
                Coverage = ToDocument(name.Coverage),
            },
            ImageAltTextMetadata alt => document with
            {
                AltText = new(alt.Suggestion == null ? null : ToDocument(alt.Suggestion)),
                Coverage = ToDocument(alt.Coverage),
            },
            CaptureSynopsisMetadata synopsis => document with
            {
                Synopsis = new(synopsis.Summary.Select(ToDocument).ToArray()),
                Coverage = ToDocument(synopsis.Coverage),
            },
            CaptureClassificationMetadata classification => document with
            {
                Classification = ToDocument(classification),
                Coverage = ToDocument(classification.Coverage),
            },
            StructuredFactsMetadata facts => document with
            {
                Facts = facts.Facts.Select(ToDocument).ToArray(),
                Coverage = ToDocument(facts.Coverage),
            },
            FileDetailsMetadata file => document with
            {
                FileDetails = ToDocument(file),
            },
            QrCodeMetadata qr => document with
            {
                QrCodes = qr.Codes.Select(ToDocument).ToArray(),
            },
            TextRecognitionMetadata text => document with
            {
                Text = text.Regions.Select(ToDocument).ToArray(),
            },
            DescriptionMetadata descriptions => document with
            {
                Descriptions = descriptions.Descriptions.Select(ToDocument).ToArray(),
            },
            TranscriptMetadata transcript => document with
            {
                Transcript = new(transcript.Language, transcript.Segments.Select(ToDocument).ToArray()),
            },
            _ => throw new InvalidDataException("Unregistered metadata payload type."),
        };
    }

    private static AnalysisResult ToResult(ResultDocument document)
    {
        if (document == null || document.SchemaVersion != 1 || document.Producer == null ||
            document.ResultId == Guid.Empty || document.ProducingRunId == null || document.ProducingRunId == Guid.Empty)
        {
            throw new InvalidDataException("Unsupported or invalid analysis result.");
        }

        AnalysisPayload payload = ToPayload(document);
        ProducerDocument producer = document.Producer;
        var provenance = new AnalyzerProvenance(producer.AnalyzerId, producer.ProviderId, producer.ModelId,
            producer.AdapterVersion, producer.ModelVersion);
        AnalysisDerivation? derivation = document.Inputs == null
            ? null
            : new AnalysisDerivation(document.Inputs.Select(ToInputReference));
        return new AnalysisResult(payload, provenance, document.GeneratedAt, document.PlanVersion,
            document.ProducingRunId, document.ResultId, derivation);
    }

    private static AnalysisPayload ToPayload(ResultDocument document)
    {
        int payloadCount =
            (document.Name != null ? 1 : 0) +
            (document.AltText != null ? 1 : 0) +
            (document.Facts != null ? 1 : 0) +
            (document.Synopsis != null ? 1 : 0) +
            (document.Classification != null ? 1 : 0) +
            (document.FileDetails != null ? 1 : 0) +
            (document.Text != null ? 1 : 0) +
            (document.Descriptions != null ? 1 : 0) +
            (document.Transcript != null ? 1 : 0) +
            (document.QrCodes != null ? 1 : 0);
        if (payloadCount != 1)
        {
            throw new InvalidDataException("Unsupported or ambiguous metadata payload.");
        }

        bool isDerived = document.Name != null || document.AltText != null || document.Facts != null ||
            document.Synopsis != null || document.Classification != null;
        if (!isDerived && document.Coverage != null)
        {
            throw new InvalidDataException("Processing coverage belongs to derived metadata.");
        }

        // Exactly one payload has been established above. Dispatch only needs to
        // check its capability and the fields required by that payload's schema.
        return document switch
        {
            { Capability: "capture-name", Name: not null, Coverage: not null } =>
                new CaptureNameMetadata(document.Name.Suggestion == null ? null : ToSuggestedText(document.Name.Suggestion), ToCoverage(document.Coverage)),
            { Capability: "image-alt-text", AltText: not null, Coverage: not null } =>
                new ImageAltTextMetadata(document.AltText.Suggestion == null ? null : ToSuggestedText(document.AltText.Suggestion), ToCoverage(document.Coverage)),
            { Capability: "capture-synopsis", Synopsis.Summary: not null, Coverage: not null } =>
                new CaptureSynopsisMetadata(document.Synopsis.Summary.Select(ToSuggestedText), ToCoverage(document.Coverage)),
            { Capability: "capture-classification", Classification: { VocabularyVersion: "1", Evidence: not null, Topics: not null }, Coverage: not null } =>
                ToClassification(document.Classification, document.Coverage),
            { Capability: "structured-facts", Coverage: not null, Facts: not null } =>
                new StructuredFactsMetadata(document.Facts.Select(ToFact), ToCoverage(document.Coverage)),
            { Capability: "file-details", FileDetails: not null } =>
                ToFileDetails(document.FileDetails),
            { Capability: "qr-code-detection", QrCodes: not null } =>
                new QrCodeMetadata(document.QrCodes.Select(ToQrCode)),
            { Capability: "text-recognition", Text: not null } =>
                new TextRecognitionMetadata(document.Text.Select(ToText)),
            { Capability: "description", Descriptions: not null } =>
                new DescriptionMetadata(document.Descriptions.Select(ToDescription)),
            { Capability: "transcription", Transcript.Segments: not null } =>
                new TranscriptMetadata(document.Transcript.Language, document.Transcript.Segments.Select(ToSegment)),
            _ => throw new InvalidDataException("Unsupported or ambiguous metadata payload."),
        };
    }

    private static ProducerDocument ToDocument(AnalyzerProvenance producer) =>
        new(producer.AnalyzerId, producer.ProviderId, producer.ModelId, producer.AdapterVersion, producer.ModelVersion);

    private static InputReferenceDocument ToDocument(AnalysisInputReference input) =>
        new(new CapabilityDocument(input.Capability.Name, input.Capability.SchemaVersion), input.ResultId);

    private static ClassificationDocument ToDocument(CaptureClassificationMetadata classification) =>
        new(VocabularyVersion: CaptureClassificationMetadata.VocabularyVersion,
            Category: classification.Category is { } category ? (int)category : null,
            Evidence: classification.CategoryEvidence.Select(ToDocument).ToArray(),
            Topics: classification.Topics.Select(ToDocument).ToArray());

    private static FactDocument ToDocument(StructuredFact fact) =>
        new((int)fact.Kind, fact.Value, fact.Evidence.Select(ToDocument).ToArray());

    private static QrCodeDocument ToDocument(DecodedQrCode code) =>
        new(code.Value, ToDocument(code.Bounds), code.Timestamp?.Ticks);

    private static TextDocument ToDocument(RecognizedText region) =>
        new(region.Text, region.Bounds is { } bounds ? ToDocument(bounds) : null,
            region.Timestamp?.Ticks, region.LineIndex, region.WordIndex);

    private static DescriptionDocument ToDocument(MediaDescription description) =>
        new(description.Text, description.Timestamp?.Ticks);

    private static SegmentDocument ToDocument(TranscriptSegment segment) =>
        new(segment.Text, segment.Start.Ticks, segment.End.Ticks);

    private static FileDetailsDocument ToDocument(FileDetailsMetadata file)
    {
        ImageFileDocument? image = file.Image is { } imageDetails
            ? new(ToDocument(imageDetails.Dimensions), imageDetails.DpiX, imageDetails.DpiY)
            : null;
        VideoFileDocument? video = file.Video is { } videoDetails
            ? new(ToDocument(videoDetails.Dimensions), videoDetails.FrameRate, videoDetails.Bitrate, videoDetails.Codec)
            : null;
        AudioFileDocument? audio = file.Audio is { } audioDetails
            ? new(audioDetails.Channels, audioDetails.SampleRate, audioDetails.Bitrate, audioDetails.Codec)
            : null;
        return new FileDetailsDocument(
            MediaKind: (int)file.MediaKind,
            FileName: file.FileName,
            SizeBytes: file.SizeBytes,
            ContentType: file.ContentType,
            FileCreatedAt: file.FileCreatedAt,
            FileModifiedAt: file.FileModifiedAt,
            CapturedAt: file.CapturedAt,
            DurationTicks: file.Duration?.Ticks,
            Image: image,
            Video: video,
            Audio: audio);
    }

    private static DimensionsDocument ToDocument(MediaDimensions dimensions) => new(dimensions.Width, dimensions.Height);

    private static BoundsDocument ToDocument(NormalizedBounds bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static CoverageDocument ToDocument(MetadataProcessingCoverage coverage) =>
        new(coverage.AvailableEntries, coverage.IncludedEntries, coverage.IncludedCharacters, (int)coverage.Limits);

    private static MetadataProcessingCoverage ToCoverage(CoverageDocument coverage) =>
        new(coverage.AvailableEntries, coverage.IncludedEntries, coverage.IncludedCharacters, (MetadataProcessingLimit)coverage.Limits);

    private static EvidenceDocument ToDocument(AnalysisEvidence evidence) => new(evidence.ResultId, evidence.EntryIndex, evidence.Start, evidence.Length);

    private static SuggestedTextDocument ToDocument(SuggestedText text) => new(text.Text, text.Evidence.Select(ToDocument).ToArray());

    private static AnalysisEvidence ToEvidence(EvidenceDocument document)
    {
        if (document == null)
        {
            throw new InvalidDataException("Missing insight evidence.");
        }
        return new(document.ResultId, document.EntryIndex, document.Start, document.Length);
    }

    private static SuggestedText ToSuggestedText(SuggestedTextDocument document)
    {
        if (document?.Evidence == null)
        {
            throw new InvalidDataException("Missing insight text or evidence.");
        }
        return new(document.Text, document.Evidence.Select(ToEvidence));
    }

    private static CaptureClassificationMetadata ToClassification(ClassificationDocument document, CoverageDocument coverage)
    {
        CaptureCategory? category = document.Category is { } value ? (CaptureCategory)value : null;
        IEnumerable<AnalysisEvidence> evidence = document.Evidence.Select(ToEvidence);
        IEnumerable<SuggestedText> topics = document.Topics.Select(ToSuggestedText);
        return new(category, evidence, topics, ToCoverage(coverage));
    }

    private static AnalysisInputReference ToInputReference(InputReferenceDocument document)
    {
        if (document?.Capability == null)
        {
            throw new InvalidDataException("Missing derived input reference.");
        }

        return new(new(document.Capability.Name, document.Capability.SchemaVersion), document.ResultId);
    }

    private static StructuredFact ToFact(FactDocument document)
    {
        if (document?.Evidence == null)
        {
            throw new InvalidDataException("Missing fact evidence.");
        }

        if (document.Evidence.Any(evidence => evidence == null))
        {
            throw new InvalidDataException("Missing evidence span.");
        }
        return new((StructuredFactKind)document.Kind, document.Value, document.Evidence.Select(ToEvidence));
    }

    private static RecognizedText ToText(TextDocument document)
    {
        if (document == null)
        {
            throw new InvalidDataException("Missing text region.");
        }

        BoundsDocument? bounds = document.Bounds;
        return new(document.Text, bounds == null ? null : new(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null, document.LineIndex, document.WordIndex);
    }

    private static FileDetailsMetadata ToFileDetails(FileDetailsDocument file)
    {
        ImageFileDetails? image = file.Image is { } imageDocument
            ? new(ToDimensions(imageDocument.Dimensions), imageDocument.DpiX, imageDocument.DpiY)
            : null;
        VideoFileDetails? video = file.Video is { } videoDocument
            ? new(ToDimensions(videoDocument.Dimensions), videoDocument.FrameRate, videoDocument.Bitrate, videoDocument.Codec)
            : null;
        AudioFileDetails? audio = file.Audio is { } audioDocument
            ? new(audioDocument.Channels, audioDocument.SampleRate, audioDocument.Bitrate, audioDocument.Codec)
            : null;
        return new FileDetailsMetadata(
            mediaKind: (AnalysisMediaKind)file.MediaKind,
            fileName: file.FileName,
            sizeBytes: file.SizeBytes,
            contentType: file.ContentType,
            fileCreatedAt: file.FileCreatedAt,
            fileModifiedAt: file.FileModifiedAt,
            capturedAt: file.CapturedAt,
            duration: file.DurationTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null,
            image: image,
            video: video,
            audio: audio);
    }

    private static MediaDimensions ToDimensions(DimensionsDocument dimensions)
    {
        if (dimensions == null)
        {
            throw new InvalidDataException("Missing media dimensions.");
        }
        return new(dimensions.Width, dimensions.Height);
    }

    private static DecodedQrCode ToQrCode(QrCodeDocument document)
    {
        if (document?.Bounds == null)
        {
            throw new InvalidDataException("Missing QR code bounds.");
        }

        BoundsDocument bounds = document.Bounds;
        return new(document.Value, new(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
    }

    private static MediaDescription ToDescription(DescriptionDocument document)
    {
        if (document == null)
        {
            throw new InvalidDataException("Missing description.");
        }

        return new(document.Text, document.TimestampTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null);
    }

    private static TranscriptSegment ToSegment(SegmentDocument document)
    {
        if (document == null)
        {
            throw new InvalidDataException("Missing transcript segment.");
        }

        return new(document.Text, TimeSpan.FromTicks(document.StartTicks), TimeSpan.FromTicks(document.EndTicks));
    }
}
