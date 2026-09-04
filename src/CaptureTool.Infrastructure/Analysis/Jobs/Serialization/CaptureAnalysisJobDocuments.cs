using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Domain.Analysis;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Jobs.Serialization;

internal sealed class CaptureAnalysisJobDocument
{
    public int SchemaVersion { get; set; }

    public required CaptureAnalysisJobIntentDocument Intent { get; set; }

    public Guid? LeaseToken { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
}

internal sealed class CaptureAnalysisJobIntentDocument
{
    public Guid? OperationId { get; set; }

    public required CaptureAnalysisJobKeyDocument Key { get; set; }

    public CaptureAnalysisJobState State { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset EnqueuedAtUtc { get; set; }

    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public AnalysisFailureCode? LatestFailureCode { get; set; }

    public AnalysisFailureDisposition? LatestFailureDisposition { get; set; }

    public List<CaptureAnalyzerAttemptDocument> Attempts { get; set; } = [];
}

internal sealed class CaptureAnalysisJobKeyDocument
{
    public required AnalysisCommitPreconditionsDocument Preconditions { get; set; }

    public required CapabilityDefinitionDocument Capability { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CapabilityDefinitionDocument>? Dependencies { get; set; }

    public ProcessingBoundary AuthorizedProcessingBoundary { get; set; }
}

internal sealed class AnalysisCommitPreconditionsDocument
{
    public required string CaptureId { get; set; }

    public long CaptureSourceGeneration { get; set; }

    public long SourceLength { get; set; }

    public DateTimeOffset SourceLastWriteTimeUtc { get; set; }

    public required string SourceFingerprint { get; set; }

    public required string PurposeId { get; set; }

    public int PurposeVersion { get; set; }

    public long PolicyRevision { get; set; }

    public long ControlGeneration { get; set; }

    public long EnrollmentGeneration { get; set; }

    public long TombstoneGeneration { get; set; }

    public required string RecipeId { get; set; }

    public int RecipeVersion { get; set; }

    public long ResolutionPolicyRevision { get; set; }
}

internal sealed class CapabilityDefinitionDocument
{
    public required string Id { get; set; }

    public int SchemaVersion { get; set; }

    public CapabilityResultClassification Classification { get; set; }
}

internal sealed class CaptureAnalyzerAttemptDocument
{
    public int AttemptNumber { get; set; }

    public required AnalyzerIdentityDocument Analyzer { get; set; }

    public ProcessingBoundary ProcessingBoundary { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public CaptureAnalyzerAttemptStatus Status { get; set; }

    public AnalysisFailureCode? FailureCode { get; set; }

    public AnalysisFailureDisposition? FailureDisposition { get; set; }
}

internal sealed class AnalyzerIdentityDocument
{
    public required string AnalyzerId { get; set; }

    public required string ProviderId { get; set; }

    public required string ModelId { get; set; }

    public required string ModelVersion { get; set; }

    public required string AdapterVersion { get; set; }

    public required string RuntimeId { get; set; }

    public required string RuntimeVersion { get; set; }

    public required string PackageVersion { get; set; }

    public required string ConfigurationFingerprint { get; set; }
}
