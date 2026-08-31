using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Analysis.Memory;

public enum CaptureMemoryOperationKind
{
    Unknown,
    Enable,
    IncludeExistingCaptures,
    StopNewCaptures,
    ResumeNewCaptures,
    TurnOffAndErase,
    ClearMemory,
    RebuildSearch,
    Reanalyze,
}

public enum CaptureMemoryOperationPhase
{
    Unknown,
    Accepted,
    Authorizing,
    PreparingModels,
    AuthorizingBackfill,
    SchedulingCaptures,
    Cleaning,
    RebuildingSearch,
    Finished,
}

public enum CaptureMemoryOperationStatus
{
    Unknown,
    Running,
    Succeeded,
    Partial,
    Cancelled,
    Conflict,
    Rejected,
    RecoveryRequired,
    Failed,
}

public sealed record CaptureMemoryOperationRequest
{
    public CaptureMemoryOperationRequest(CaptureMemoryOperationKind kind, bool includeExistingCaptures = false)
    {
        if (!Enum.IsDefined(kind) || kind == CaptureMemoryOperationKind.Unknown ||
            includeExistingCaptures && kind != CaptureMemoryOperationKind.Enable)
        {
            throw new ArgumentException("Only enabling Memory accepts an existing-capture opt-in.", nameof(kind));
        }
        Kind = kind;
        IncludeExistingCaptures = includeExistingCaptures;
    }

    public CaptureMemoryOperationKind Kind { get; }
    public bool IncludeExistingCaptures { get; }
}

/// <summary>
/// Persisted command intent, not a claim that queued AI jobs have completed. Contains no media
/// paths or recognized content. The policy epoch prevents restart recovery from restoring consent.
/// </summary>
public sealed record CaptureMemoryOperation
{
    public CaptureMemoryOperation(Guid id, CaptureMemoryOperationRequest request, DateTimeOffset startedAtUtc,
        long controlGeneration, long policyRevision, CaptureMemoryOperationPhase phase,
        CaptureMemoryOperationStatus status, IEnumerable<CaptureId>? captureIds = null,
        int affectedCaptureCount = 0, bool hasLimitedModelCoverage = false, bool isSchedulingComplete = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (id == Guid.Empty || startedAtUtc.Offset != TimeSpan.Zero || controlGeneration < 0 || policyRevision < 0 ||
            !Enum.IsDefined(phase) || phase == CaptureMemoryOperationPhase.Unknown ||
            !Enum.IsDefined(status) || status == CaptureMemoryOperationStatus.Unknown || affectedCaptureCount < 0 ||
            (phase == CaptureMemoryOperationPhase.Finished) != (status != CaptureMemoryOperationStatus.Running))
        {
            throw new ArgumentException("Invalid Capture Memory operation state.");
        }
        CaptureId[] targets = [.. captureIds ?? []];
        if (targets.Any(id => id.IsEmpty) || targets.Distinct().Count() != targets.Length ||
            targets.Length > 10_000 || targets.Length > 0 && request.Kind != CaptureMemoryOperationKind.Reanalyze)
        {
            throw new ArgumentException("Only reanalysis may retain distinct bounded capture identities.", nameof(captureIds));
        }
        Id = id;
        Request = request;
        StartedAtUtc = startedAtUtc;
        ControlGeneration = controlGeneration;
        PolicyRevision = policyRevision;
        Phase = phase;
        Status = status;
        CaptureIds = Array.AsReadOnly(targets);
        AffectedCaptureCount = affectedCaptureCount;
        HasLimitedModelCoverage = hasLimitedModelCoverage;
        IsSchedulingComplete = isSchedulingComplete;
    }

    public Guid Id { get; }
    public CaptureMemoryOperationRequest Request { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public long ControlGeneration { get; }
    public long PolicyRevision { get; }
    public CaptureMemoryOperationPhase Phase { get; }
    public CaptureMemoryOperationStatus Status { get; }
    public IReadOnlyList<CaptureId> CaptureIds { get; }
    public int AffectedCaptureCount { get; }
    public bool HasLimitedModelCoverage { get; }
    public bool IsSchedulingComplete { get; }
    public bool IsRunning => Status == CaptureMemoryOperationStatus.Running;

    public CaptureMemoryOperation Advance(CaptureMemoryOperationPhase phase,
        CaptureMemoryOperationStatus status = CaptureMemoryOperationStatus.Running,
        long? controlGeneration = null, long? policyRevision = null,
        int? affectedCaptureCount = null, bool? hasLimitedModelCoverage = null, bool? isSchedulingComplete = null) => new(
            Id, Request, StartedAtUtc, controlGeneration ?? ControlGeneration, policyRevision ?? PolicyRevision,
            phase, status, CaptureIds, affectedCaptureCount ?? AffectedCaptureCount,
            hasLimitedModelCoverage ?? HasLimitedModelCoverage, isSchedulingComplete ?? IsSchedulingComplete);
}

public sealed record CaptureMemoryOperationSnapshot(long Revision, CaptureMemoryOperation? Operation);

public interface ICaptureMemoryOperationStore
{
    ValueTask<CaptureMemoryOperationSnapshot> GetAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> TryWriteAsync(CaptureMemoryOperation operation, long expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed record CaptureMemoryWorkflowSnapshot(
    CaptureAnalysisPolicySnapshot Policy, CaptureMemoryOperation? Operation, double FractionComplete = 0)
{
    public bool IsBusy => Operation?.IsRunning == true;
}

public interface ICaptureMemoryWorkflow
{
    event EventHandler? Changed;
    ValueTask<CaptureMemoryWorkflowSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<CaptureMemoryOperation> ExecuteAsync(CaptureMemoryOperationRequest request,
        CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    void Cancel(Guid operationId);
}
