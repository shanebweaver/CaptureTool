using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Analysis.Maintenance;

public enum CaptureAnalysisMaintenanceStatus
{
    Unknown,
    Succeeded,
    Rejected,
    Conflict,
    Incomplete,
    Unavailable,
}

public enum CaptureAnalysisMaintenancePhase
{
    Unknown,
    PreparingModels,
    SchedulingCaptures,
}

public sealed record CaptureAnalysisMaintenanceProgress
{
    public CaptureAnalysisMaintenanceProgress(
        CaptureAnalysisMaintenancePhase phase,
        double fractionComplete)
    {
        if (!Enum.IsDefined(phase) || phase == CaptureAnalysisMaintenancePhase.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        if (!double.IsFinite(fractionComplete) || fractionComplete is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fractionComplete));
        }

        Phase = phase;
        FractionComplete = fractionComplete;
    }

    public CaptureAnalysisMaintenancePhase Phase { get; }

    public double FractionComplete { get; }
}

public sealed record CaptureAnalysisMaintenanceResult
{
    public CaptureAnalysisMaintenanceResult(
        CaptureAnalysisMaintenanceStatus status,
        int affectedCaptureCount = 0)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisMaintenanceStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (affectedCaptureCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(affectedCaptureCount));
        }

        if (status is (CaptureAnalysisMaintenanceStatus.Rejected or
            CaptureAnalysisMaintenanceStatus.Conflict or
            CaptureAnalysisMaintenanceStatus.Unavailable) && affectedCaptureCount != 0)
        {
            throw new ArgumentException(
                "A maintenance operation that did not begin cannot report affected captures.",
                nameof(affectedCaptureCount));
        }

        Status = status;
        AffectedCaptureCount = affectedCaptureCount;
    }

    public CaptureAnalysisMaintenanceStatus Status { get; }

    public int AffectedCaptureCount { get; }
}

public enum CaptureAnalysisReanalysisScope
{
    Unknown,
    AllEnrolledCaptures,
    SelectedCaptures,
}

public sealed record CaptureAnalysisReanalysisRequest
{
    private const int MaximumSelectedCaptureCount = 10_000;
    private readonly IReadOnlyList<CaptureId> _captureIds;

    public CaptureAnalysisReanalysisRequest(
        CaptureAnalysisReanalysisScope scope,
        IEnumerable<CaptureId>? captureIds = null)
    {
        if (!Enum.IsDefined(scope) || scope == CaptureAnalysisReanalysisScope.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        CaptureId[] copiedCaptureIds = [.. captureIds ?? []];
        if (copiedCaptureIds.Any(captureId => captureId.IsEmpty) ||
            copiedCaptureIds.Distinct().Count() != copiedCaptureIds.Length ||
            copiedCaptureIds.Length > MaximumSelectedCaptureCount)
        {
            throw new ArgumentException(
                "Selected captures must contain distinct, non-empty IDs within the supported bound.",
                nameof(captureIds));
        }

        bool hasSelectedCaptures = copiedCaptureIds.Length > 0;
        if ((scope == CaptureAnalysisReanalysisScope.SelectedCaptures) != hasSelectedCaptures)
        {
            throw new ArgumentException(
                "Selected reanalysis requires capture IDs, while all-capture reanalysis cannot contain them.",
                nameof(captureIds));
        }

        Scope = scope;
        _captureIds = Array.AsReadOnly(copiedCaptureIds);
    }

    public CaptureAnalysisReanalysisScope Scope { get; }

    public IReadOnlyList<CaptureId> CaptureIds => _captureIds;
}

public interface ICaptureAnalysisMaintenanceService
{
    ValueTask<CaptureAnalysisMaintenanceResult> ClearMemoryAsync(
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisMaintenanceResult> RebuildSearchIndexAsync(
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
        CaptureAnalysisReanalysisRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
        CaptureAnalysisReanalysisRequest request,
        IProgress<CaptureAnalysisMaintenanceProgress> progress,
        CancellationToken cancellationToken = default);
}
