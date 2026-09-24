using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record AnalysisAdmissionScope(Guid Generation, long ReconciliationBoundary);
public sealed record AnalysisRunToken(CaptureId CaptureId, Guid Generation, Guid RunId);
public sealed record AnalysisRequest(CaptureId CaptureId, AnalysisMediaKind MediaKind, string SourcePath,
    Guid RequestId, Guid Generation, Guid? ExpectedRunId = null, string? Language = null);
public sealed record AnalysisWorkItem(AnalysisRunToken Token, AnalysisMediaKind MediaKind, string SourcePath,
    string? Language, AnalysisRun Run);

/// <summary>Execution operations on the same singleton protected metadata store, not another queue database.</summary>
public interface IAnalysisExecutionStore : ICaptureAnalysisStore
{
    Task<AnalysisAdmissionScope> GetAdmissionScopeAsync(CancellationToken cancellationToken = default);
    Task<AnalysisWorkItem?> GetWorkAsync(CaptureId captureId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnalysisWorkItem>> ReadPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> AdmitAsync(AnalysisRequest request, Guid authorizationId, MediaAnalysisPlan plan, CancellationToken cancellationToken = default);
    Task<bool> BindSourceAsync(AnalysisRunToken token, SourceRevision revision, CancellationToken cancellationToken = default);
    Task<bool> CommitStepAsync(AnalysisRunToken token, AnalysisStepCompletion step, AnalysisResult? result,
        CancellationToken cancellationToken = default);
    Task<bool> FinishAsync(AnalysisRunToken token, AnalysisRunStatus status, CancellationToken cancellationToken = default);

    /// <summary>Atomically fences work and records the caller's capture-catalog watermark for later reconciliation.</summary>
    Task<AnalysisCleanupResult> ClearAsync(long reconciliationBoundary, CancellationToken cancellationToken = default);
}
