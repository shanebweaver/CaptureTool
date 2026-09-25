using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal sealed record AnalysisControlDocument(int Version, Guid Generation, long QueueOrder = 0, long ReconciliationBoundary = 0);

internal sealed record AnalysisDocument(int Version, Guid CaptureId, int MediaKind, string? SourceSha256,
    string PlanVersion, Guid RunId, ResultDocument[] Results, RunDocument? Run = null);

internal sealed record ResultDocument(string Capability, int SchemaVersion, ProducerDocument Producer,
    DateTimeOffset GeneratedAt, string PlanVersion, TextDocument[]? Text, DescriptionDocument[]? Descriptions,
    TranscriptDocument? Transcript, Guid? ProducingRunId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] QrCodeDocument[]? QrCodes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FileDetailsDocument? FileDetails = null);

internal sealed record RunDocument(Guid Id, Guid AuthorizationId, long QueueOrder, string PlanVersion,
    string SourcePath, string? Language, string? SourceSha256, int Status, CapabilityDocument[] Steps,
    StepCompletionDocument[] CompletedSteps);
internal sealed record CapabilityDocument(string Name, int SchemaVersion);
internal sealed record StepCompletionDocument(CapabilityDocument Capability, int Outcome, string? FailureCode);

internal sealed record ProducerDocument(string AnalyzerId, string ProviderId, string ModelId,
    string AdapterVersion, string? ModelVersion);

internal sealed record BoundsDocument(double X, double Y, double Width, double Height);
internal sealed record TextDocument(string Text, BoundsDocument? Bounds, long? TimestampTicks);
internal sealed record QrCodeDocument(string Value, BoundsDocument Bounds, long? TimestampTicks);
internal sealed record DescriptionDocument(string Text, long? TimestampTicks);
internal sealed record TranscriptDocument(string? Language, SegmentDocument[] Segments);
internal sealed record SegmentDocument(string Text, long StartTicks, long EndTicks);

internal sealed record FileDetailsDocument(int MediaKind, string FileName, long SizeBytes, string? ContentType,
    DateTimeOffset FileCreatedAt, DateTimeOffset FileModifiedAt, DateTimeOffset? CapturedAt, long? DurationTicks,
    ImageFileDocument? Image, VideoFileDocument? Video, AudioFileDocument? Audio, bool CaptureTimeVerified = false);
internal sealed record DimensionsDocument(uint Width, uint Height);
internal sealed record ImageFileDocument(DimensionsDocument Dimensions, double? DpiX, double? DpiY);
internal sealed record VideoFileDocument(DimensionsDocument Dimensions, double? FrameRate, uint? Bitrate, string? Codec);
internal sealed record AudioFileDocument(uint? Channels, uint? SampleRate, uint? Bitrate, string? Codec);
