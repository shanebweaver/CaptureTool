using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public enum AnalysisActivity { Idle, Preparing, Analyzing, StorageUnavailable, ProviderUnavailable }
public sealed record AnalysisActivitySnapshot(AnalysisActivity Activity, int QueuedCaptures = 0,
    CaptureId? CaptureId = null, int CompletedSteps = 0, int TotalSteps = 0, double? Fraction = null, string? FailureCode = null,
    AnalysisRunStatus? LastRunStatus = null, bool LastRunHadFailures = false);

public interface ICaptureAnalysisWorker
{
    AnalysisActivitySnapshot Progress { get; }
    /// <summary>Ordered notifications of the latest snapshot; concurrent intermediate updates may be coalesced.</summary>
    event Action<AnalysisActivitySnapshot>? ProgressChanged;
    /// <summary>Successful durable result publication. Observers enqueue work and recover missed notifications on startup.</summary>
    event Action<CaptureId, AnalysisCapability>? ResultCommitted;
    Task<bool> EnqueueAsync(AnalysisRequest request, CancellationToken cancellationToken = default);
    /// <summary>One application-lifetime loop. Cancellation suspends unfinished work for a future restart.</summary>
    Task RunAsync(CancellationToken cancellationToken);
    Task CancelAsync(CaptureId captureId, CancellationToken cancellationToken = default);
    Task<AnalysisCleanupResult> ClearAsync(long reconciliationBoundary, CancellationToken cancellationToken = default);
}
