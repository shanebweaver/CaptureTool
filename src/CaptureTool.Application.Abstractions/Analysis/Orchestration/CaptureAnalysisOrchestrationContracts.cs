using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Orchestration;

public sealed record CaptureAnalysisScheduleRequest
{
    private readonly IReadOnlyList<RecipeCapability> _capabilities;

    public CaptureAnalysisScheduleRequest(
        CaptureAnalysisAdmissionRequest admission,
        CaptureAnalysisRecipe recipe,
        ProcessingBoundary processingBoundary,
        bool forceReanalysis = false,
        Guid? operationId = null,
        IEnumerable<AnalysisCapabilityId>? capabilityIds = null)
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
        if (operationId == Guid.Empty || operationId.HasValue && !forceReanalysis)
        {
            throw new ArgumentException("An operation identity requires explicit reanalysis.", nameof(operationId));
        }
        OperationId = operationId;

        AnalysisCapabilityId[] selectedCapabilityIds = [.. capabilityIds ?? []];
        if (selectedCapabilityIds.Any(capabilityId => capabilityId.IsEmpty) ||
            selectedCapabilityIds.Distinct().Count() != selectedCapabilityIds.Length)
        {
            throw new ArgumentException(
                "Selected capabilities must contain distinct, non-empty IDs.",
                nameof(capabilityIds));
        }

        RecipeCapability[] selectedCapabilities = selectedCapabilityIds.Length == 0
            ? [.. recipe.Capabilities]
            : recipe.Capabilities
                .Where(capability => selectedCapabilityIds.Contains(capability.Capability.Id))
                .ToArray();
        if (selectedCapabilities.Length == 0 ||
            selectedCapabilityIds.Length > 0 &&
            selectedCapabilities.Length != selectedCapabilityIds.Length)
        {
            throw new ArgumentException(
                "Every selected capability must belong to the analysis recipe.",
                nameof(capabilityIds));
        }

        _capabilities = Array.AsReadOnly(selectedCapabilities);
    }

    public CaptureAnalysisAdmissionRequest Admission { get; }

    public CaptureAnalysisRecipe Recipe { get; }

    public ProcessingBoundary ProcessingBoundary { get; }

    public bool ForceReanalysis { get; }

    public Guid? OperationId { get; }

    public IReadOnlyList<RecipeCapability> Capabilities => _capabilities;
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
