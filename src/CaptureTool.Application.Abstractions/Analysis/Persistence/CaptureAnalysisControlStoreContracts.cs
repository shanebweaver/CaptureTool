using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Persistence;

public enum CaptureAnalysisEnrollmentState
{
    Unknown,
    Enrolled,
    Excluded,
    Forgotten,
}

public enum CaptureAnalysisExclusionReason
{
    None,
    UserExcluded,
    PrivateCapture,
    IneligibleOrigin,
    MissingSource,
    SourceDeleted,
    HistoryForgotten,
    DeleteRequested,
    MemoryCleared,
}

public sealed record CaptureAnalysisEnrollment
{
    public CaptureAnalysisEnrollment(
        CaptureId captureId,
        CaptureAnalysisEnrollmentState state,
        CaptureAnalysisExclusionReason exclusionReason,
        long enrollmentGeneration,
        long tombstoneGeneration,
        long assetFinalizationSequence,
        AnalysisRecipeId? requestedRecipeId,
        AnalysisRecipeVersion? requestedRecipeVersion)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("An enrollment requires a capture ID.", nameof(captureId));
        }

        if (!Enum.IsDefined(state) || state == CaptureAnalysisEnrollmentState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (!Enum.IsDefined(exclusionReason))
        {
            throw new ArgumentOutOfRangeException(nameof(exclusionReason));
        }

        if (enrollmentGeneration <= 0 || tombstoneGeneration < 0 || assetFinalizationSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enrollmentGeneration),
                "Enrollment revisions must be valid positive or non-negative values.");
        }

        bool hasRecipeId = requestedRecipeId is { IsEmpty: false };
        bool hasRecipeVersion = requestedRecipeVersion is { IsEmpty: false };
        if ((requestedRecipeId.HasValue && !hasRecipeId) ||
            (requestedRecipeVersion.HasValue && !hasRecipeVersion))
        {
            throw new ArgumentException("A requested recipe cannot contain an empty ID or version.");
        }

        if (hasRecipeId != hasRecipeVersion)
        {
            throw new ArgumentException("A requested recipe requires both an ID and version.");
        }

        bool hasRecipe = hasRecipeId && hasRecipeVersion;
        if (state == CaptureAnalysisEnrollmentState.Enrolled &&
            (!hasRecipe || exclusionReason != CaptureAnalysisExclusionReason.None))
        {
            throw new ArgumentException("An enrolled capture requires a recipe and cannot be excluded.");
        }

        if (state != CaptureAnalysisEnrollmentState.Enrolled && hasRecipe)
        {
            throw new ArgumentException("Excluded and forgotten captures cannot retain a requested recipe.");
        }

        if (state == CaptureAnalysisEnrollmentState.Excluded &&
            exclusionReason == CaptureAnalysisExclusionReason.None)
        {
            throw new ArgumentException("An excluded capture requires a bounded exclusion reason.");
        }

        if (state == CaptureAnalysisEnrollmentState.Forgotten && tombstoneGeneration == 0)
        {
            throw new ArgumentException("A forgotten capture requires a tombstone generation.");
        }

        CaptureId = captureId;
        State = state;
        ExclusionReason = exclusionReason;
        EnrollmentGeneration = enrollmentGeneration;
        TombstoneGeneration = tombstoneGeneration;
        AssetFinalizationSequence = assetFinalizationSequence;
        RequestedRecipeId = requestedRecipeId;
        RequestedRecipeVersion = requestedRecipeVersion;
    }

    public CaptureId CaptureId { get; }

    public CaptureAnalysisEnrollmentState State { get; }

    public CaptureAnalysisExclusionReason ExclusionReason { get; }

    // Eligibility is shared by maintenance, admission, and UI read models. A global clear
    // is recoverable with explicit consent; privacy/removal exclusions are not.
    public bool IsMemoryCleared => State == CaptureAnalysisEnrollmentState.Excluded &&
        ExclusionReason == CaptureAnalysisExclusionReason.MemoryCleared;

    public bool CanReanalyze => State == CaptureAnalysisEnrollmentState.Enrolled || IsMemoryCleared;

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    // The immutable sequence of the asset's original Finalized fact. Later location/source changes
    // cannot turn a pre-watermark asset into a future capture.
    public long AssetFinalizationSequence { get; }

    public AnalysisRecipeId? RequestedRecipeId { get; }

    public AnalysisRecipeVersion? RequestedRecipeVersion { get; }
}

public sealed record CaptureAnalysisControlState
{
    private readonly IReadOnlyList<CaptureAnalysisEnrollment> _enrollments;

    public CaptureAnalysisControlState(
        CaptureAnalysisPolicy policy,
        IEnumerable<CaptureAnalysisEnrollment> enrollments,
        long captureChangeCheckpoint = 0)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (captureChangeCheckpoint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(captureChangeCheckpoint));
        }

        ArgumentNullException.ThrowIfNull(enrollments);
        CaptureAnalysisEnrollment[] copiedEnrollments = [.. enrollments];
        if (copiedEnrollments.Any(enrollment => enrollment == null))
        {
            throw new ArgumentException("Control state enrollments cannot contain null values.", nameof(enrollments));
        }

        if (copiedEnrollments.Select(enrollment => enrollment.CaptureId).Distinct().Count() !=
            copiedEnrollments.Length)
        {
            throw new ArgumentException("Control state cannot contain duplicate capture enrollments.", nameof(enrollments));
        }

        if (!policy.IsProcessingAuthorized && copiedEnrollments.Any(
            enrollment => enrollment.State == CaptureAnalysisEnrollmentState.Enrolled))
        {
            throw new ArgumentException(
                "A non-authorizing policy cannot retain active capture enrollments.",
                nameof(enrollments));
        }

        Policy = policy;
        _enrollments = Array.AsReadOnly(copiedEnrollments);
        CaptureChangeCheckpoint = captureChangeCheckpoint;
    }

    public CaptureAnalysisPolicy Policy { get; }

    public CaptureAnalysisConsentState ConsentState => Policy.ConsentState;

    public bool IsFutureCaptureAdmissionEnabled => Policy.IsFutureCaptureAdmissionEnabled;

    public long ControlGeneration => Policy.ControlGeneration;

    public long PolicyRevision => Policy.PolicyRevision;

    public CaptureAnalysisAuthorizationScope? AuthorizationScope => Policy.AuthorizationScope;

    public AnalysisPurpose? AuthorizedPurpose => Policy.AuthorizedPurpose;

    public AnalysisProcessingPolicy? ProcessingPolicy => Policy.ProcessingPolicy;

    public long FutureCaptureSequenceWatermark => Policy.FutureCaptureSequenceWatermark;

    public long BackfillCheckpoint => Policy.BackfillCheckpoint;

    public CaptureAnalysisBackfillState BackfillState => Policy.BackfillState;

    public long BackfillUpperSequence => Policy.BackfillUpperSequence;

    // Independent from the explicit existing-capture backfill checkpoint. This cursor drains the
    // durable Capture change feed used for new-capture handoff and location/source reconciliation.
    public long CaptureChangeCheckpoint { get; }

    public IReadOnlyList<CaptureAnalysisEnrollment> Enrollments => _enrollments;
}

public sealed record CaptureAnalysisControlSnapshot
{
    public CaptureAnalysisControlSnapshot(long documentRevision, CaptureAnalysisControlState state)
    {
        if (documentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentRevision));
        }

        ArgumentNullException.ThrowIfNull(state);
        DocumentRevision = documentRevision;
        State = state;
    }

    public long DocumentRevision { get; }

    public CaptureAnalysisControlState State { get; }
}

public enum CaptureAnalysisControlWriteStatus
{
    Unknown,
    Succeeded,
    Conflict,
    ReadOnlyVersion,
    Unavailable,
}

public sealed record CaptureAnalysisControlWriteResult
{
    public CaptureAnalysisControlWriteResult(
        CaptureAnalysisControlWriteStatus status,
        CaptureAnalysisControlSnapshot? snapshot = null)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisControlWriteStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if ((status is CaptureAnalysisControlWriteStatus.Succeeded or
            CaptureAnalysisControlWriteStatus.Conflict) && snapshot == null)
        {
            throw new ArgumentException(
                "A successful or conflicting control write requires the current snapshot.",
                nameof(snapshot));
        }

        Status = status;
        Snapshot = snapshot;
    }

    public CaptureAnalysisControlWriteStatus Status { get; }

    public CaptureAnalysisControlSnapshot? Snapshot { get; }
}

public interface ICaptureAnalysisControlStore
{
    ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
        CancellationToken cancellationToken = default);

    // Conflict must return the winning current snapshot. Once a write is durably committed,
    // implementations return its result even if cancellation is requested concurrently.
    ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
        CaptureAnalysisControlState state,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default);
}
