using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Orchestration;

public sealed record CaptureAnalysisScheduleRequest
{
    public CaptureAnalysisScheduleRequest(
        CaptureAnalysisAdmissionRequest admission,
        CaptureAnalysisRecipe recipe,
        ProcessingBoundary processingBoundary,
        bool forceReanalysis = false)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(recipe);
        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        Admission = admission;
        Recipe = recipe;
        ProcessingBoundary = processingBoundary;
        ForceReanalysis = forceReanalysis;
    }

    public CaptureAnalysisAdmissionRequest Admission { get; }

    public CaptureAnalysisRecipe Recipe { get; }

    public ProcessingBoundary ProcessingBoundary { get; }

    public bool ForceReanalysis { get; }
}

public enum CaptureAnalysisScheduleStatus
{
    Unknown,
    Scheduled,
    AlreadyScheduled,
    Denied,
    SourceUnavailable,
    Conflict,
    Unavailable,
}

public sealed record CaptureAnalysisScheduleResult
{
    public CaptureAnalysisScheduleResult(
        CaptureAnalysisScheduleStatus status,
        int durableIntentCount = 0,
        CaptureAnalysisPolicyDenialReason denialReason = CaptureAnalysisPolicyDenialReason.None)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisScheduleStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (durableIntentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durableIntentCount));
        }

        bool isDenied = status == CaptureAnalysisScheduleStatus.Denied;
        bool hasDenial = denialReason is not
            (CaptureAnalysisPolicyDenialReason.Unknown or CaptureAnalysisPolicyDenialReason.None);
        if (isDenied != hasDenial ||
            (status is not (CaptureAnalysisScheduleStatus.Scheduled or
                CaptureAnalysisScheduleStatus.AlreadyScheduled) && durableIntentCount != 0))
        {
            throw new ArgumentException("The schedule status, intent count, and denial reason disagree.");
        }

        Status = status;
        DurableIntentCount = durableIntentCount;
        DenialReason = denialReason;
    }

    public CaptureAnalysisScheduleStatus Status { get; }

    public int DurableIntentCount { get; }

    public CaptureAnalysisPolicyDenialReason DenialReason { get; }
}

public interface ICaptureAnalysisScheduler
{
    ValueTask<CaptureAnalysisScheduleResult> ScheduleAsync(
        CaptureAnalysisScheduleRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalysisProjectionRefresher
{
    ValueTask RefreshAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);
}

// The concrete search projection is supplied by the indexing feature. Lifecycle commands depend
// only on this metadata-only boundary and never read a capture source or invoke an analyzer.
public interface ICaptureAnalysisProjectionMaintenance
{
    ValueTask RemoveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    ValueTask<int> RebuildAsync(CancellationToken cancellationToken = default);
}
