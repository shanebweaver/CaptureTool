using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

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
    private const int MaximumSelectedCapabilityCount = 64;
    private readonly IReadOnlyList<CaptureId> _captureIds;
    private readonly IReadOnlyList<AnalysisCapabilityId> _capabilityIds;

    public CaptureAnalysisReanalysisRequest(
        CaptureAnalysisReanalysisScope scope,
        IEnumerable<CaptureId>? captureIds = null,
        Guid? operationId = null,
        IEnumerable<AnalysisCapabilityId>? capabilityIds = null)
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

        AnalysisCapabilityId[] copiedCapabilityIds = [.. capabilityIds ?? []];
        if (copiedCapabilityIds.Any(capabilityId => capabilityId.IsEmpty) ||
            copiedCapabilityIds.Distinct().Count() != copiedCapabilityIds.Length ||
            copiedCapabilityIds.Length > MaximumSelectedCapabilityCount)
        {
            throw new ArgumentException(
                "Selected capabilities must contain distinct, non-empty IDs within the supported bound.",
                nameof(capabilityIds));
        }

        Scope = scope;
        if (operationId == Guid.Empty) { throw new ArgumentException("Operation identity must be nonempty.", nameof(operationId)); }
        OperationId = operationId;
        _captureIds = Array.AsReadOnly(copiedCaptureIds);
        _capabilityIds = Array.AsReadOnly(copiedCapabilityIds);
    }

    public CaptureAnalysisReanalysisScope Scope { get; }

    public Guid? OperationId { get; }

    public IReadOnlyList<CaptureId> CaptureIds => _captureIds;

    public IReadOnlyList<AnalysisCapabilityId> CapabilityIds => _capabilityIds;
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
