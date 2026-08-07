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
}

public enum CaptureAnalysisProcessingState
{
    Unknown,
    Disabled,
    Enabled,
}

public enum CaptureAnalysisBackfillState
{
    Unknown,
    NotAuthorized,
    Authorized,
    InProgress,
    Completed,
}

public sealed record CaptureAnalysisEnrollment
{
    public CaptureAnalysisEnrollment(
        CaptureId captureId,
        CaptureAnalysisEnrollmentState state,
        CaptureAnalysisExclusionReason exclusionReason,
        long enrollmentGeneration,
        long tombstoneGeneration,
        long assetChangeSequence,
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

        if (enrollmentGeneration <= 0 || tombstoneGeneration < 0 || assetChangeSequence < 0)
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
        AssetChangeSequence = assetChangeSequence;
        RequestedRecipeId = requestedRecipeId;
        RequestedRecipeVersion = requestedRecipeVersion;
    }

    public CaptureId CaptureId { get; }

    public CaptureAnalysisEnrollmentState State { get; }

    public CaptureAnalysisExclusionReason ExclusionReason { get; }

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    public long AssetChangeSequence { get; }

    public AnalysisRecipeId? RequestedRecipeId { get; }

    public AnalysisRecipeVersion? RequestedRecipeVersion { get; }
}

public sealed record CaptureAnalysisControlState
{
    private readonly IReadOnlyList<CaptureAnalysisEnrollment> _enrollments;

    public CaptureAnalysisControlState(
        CaptureAnalysisProcessingState processingState,
        bool isFutureCaptureAdmissionEnabled,
        long controlGeneration,
        long policyRevision,
        AnalysisPurpose? authorizedPurpose,
        AnalysisProcessingPolicy? processingPolicy,
        long futureCaptureSequenceWatermark,
        long backfillCheckpoint,
        CaptureAnalysisBackfillState backfillState,
        IEnumerable<CaptureAnalysisEnrollment> enrollments)
    {
        if (!Enum.IsDefined(processingState) || processingState == CaptureAnalysisProcessingState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingState));
        }

        if (controlGeneration < 0 || policyRevision < 0 ||
            futureCaptureSequenceWatermark < 0 || backfillCheckpoint < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(controlGeneration),
                "Control revisions and checkpoints cannot be negative.");
        }

        if (!Enum.IsDefined(backfillState) || backfillState == CaptureAnalysisBackfillState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(backfillState));
        }

        bool hasPurpose = authorizedPurpose is { IsEmpty: false };
        bool hasPolicy = processingPolicy != null;
        if (authorizedPurpose.HasValue && !hasPurpose)
        {
            throw new ArgumentException("An authorized purpose cannot be empty.", nameof(authorizedPurpose));
        }

        if (hasPurpose != hasPolicy)
        {
            throw new ArgumentException("Purpose and processing policy authorization must be present together.");
        }

        bool hasAuthorization = hasPurpose && hasPolicy;
        if (processingState == CaptureAnalysisProcessingState.Enabled &&
            (!hasAuthorization || processingPolicy!.AuthorizedPurpose != authorizedPurpose))
        {
            throw new ArgumentException(
                "Enabled admission requires one consistent authorized purpose and processing policy.");
        }

        if (processingState == CaptureAnalysisProcessingState.Enabled && policyRevision == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Enabled processing requires a positive policy revision.");
        }

        if (processingState == CaptureAnalysisProcessingState.Disabled && hasAuthorization)
        {
            throw new ArgumentException("Disabled admission cannot retain processing authorization.");
        }

        if (processingState == CaptureAnalysisProcessingState.Disabled &&
            backfillState != CaptureAnalysisBackfillState.NotAuthorized)
        {
            throw new ArgumentException("Disabled admission cannot authorize backfill.");
        }

        if (processingState == CaptureAnalysisProcessingState.Disabled && isFutureCaptureAdmissionEnabled)
        {
            throw new ArgumentException("Disabled processing cannot admit future captures.");
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

        ProcessingState = processingState;
        IsFutureCaptureAdmissionEnabled = isFutureCaptureAdmissionEnabled;
        ControlGeneration = controlGeneration;
        PolicyRevision = policyRevision;
        AuthorizedPurpose = authorizedPurpose;
        ProcessingPolicy = processingPolicy;
        FutureCaptureSequenceWatermark = futureCaptureSequenceWatermark;
        BackfillCheckpoint = backfillCheckpoint;
        BackfillState = backfillState;
        _enrollments = Array.AsReadOnly(copiedEnrollments);
    }

    public CaptureAnalysisProcessingState ProcessingState { get; }

    public bool IsFutureCaptureAdmissionEnabled { get; }

    public long ControlGeneration { get; }

    public long PolicyRevision { get; }

    public AnalysisPurpose? AuthorizedPurpose { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy { get; }

    public long FutureCaptureSequenceWatermark { get; }

    public long BackfillCheckpoint { get; }

    public CaptureAnalysisBackfillState BackfillState { get; }

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

        if (status == CaptureAnalysisControlWriteStatus.Succeeded && snapshot == null)
        {
            throw new ArgumentException("A successful control write requires a snapshot.", nameof(snapshot));
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

    ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
        CaptureAnalysisControlState state,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default);
}
