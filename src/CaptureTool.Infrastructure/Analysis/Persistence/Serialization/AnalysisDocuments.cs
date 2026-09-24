namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal sealed record AnalysisControlDocument(int Version, Guid Generation);

internal sealed record AnalysisDocument(int Version, Guid CaptureId, int MediaKind, string SourceSha256,
    string PlanVersion, Guid RunId, ResultDocument[] Results);

internal sealed record ResultDocument(string Capability, int SchemaVersion, ProducerDocument Producer,
    DateTimeOffset GeneratedAt, string PlanVersion, TextDocument[]? Text, DescriptionDocument[]? Descriptions,
    TranscriptDocument? Transcript);

internal sealed record ProducerDocument(string AnalyzerId, string ProviderId, string ModelId,
    string AdapterVersion, string? ModelVersion);

internal sealed record BoundsDocument(double X, double Y, double Width, double Height);
internal sealed record TextDocument(string Text, BoundsDocument? Bounds, long? TimestampTicks);
internal sealed record DescriptionDocument(string Text, long? TimestampTicks);
internal sealed record TranscriptDocument(string? Language, SegmentDocument[] Segments);
internal sealed record SegmentDocument(string Text, long StartTicks, long EndTicks);
