using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Policy;

public enum CaptureAnalysisAuthorizationStage
{
    Unknown,
    Enrollment,
    SourceVerification,
    AnalyzerAvailability,
    AnalyzerInvocation,
    CapabilityCommit,
}

public enum CaptureAnalysisPolicyDenialReason
{
    Unknown,
    None,
    FeatureDisabled,
    AnalysisDisabled,
    PurposeNotAuthorized,
    CaptureNotEnrolled,
    CaptureExcluded,
    CaptureForgotten,
    BoundaryNotAuthorized,
    ProviderNotAuthorized,
    StalePolicy,
    StaleControlGeneration,
}

public sealed record CaptureAnalysisAuthorizationRequest
{
    public CaptureAnalysisAuthorizationRequest(
        CaptureId captureId,
        AnalysisPurpose purpose,
        ProcessingBoundary processingBoundary,
        AnalyzerIdentity? analyzer,
        CaptureAnalysisAuthorizationStage stage)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Authorization requires a capture ID.", nameof(captureId));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("Authorization requires a purpose.", nameof(purpose));
        }

        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        if (!Enum.IsDefined(stage) || stage == CaptureAnalysisAuthorizationStage.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        if (processingBoundary == ProcessingBoundary.Remote && analyzer == null)
        {
            throw new ArgumentException("Remote authorization requires an exact provider identity.", nameof(analyzer));
        }

        if (stage is CaptureAnalysisAuthorizationStage.AnalyzerAvailability or
            CaptureAnalysisAuthorizationStage.AnalyzerInvocation or
            CaptureAnalysisAuthorizationStage.CapabilityCommit && analyzer == null)
        {
            throw new ArgumentException("Analyzer authorization requires an exact analyzer identity.", nameof(analyzer));
        }

        CaptureId = captureId;
        Purpose = purpose;
        ProcessingBoundary = processingBoundary;
        Analyzer = analyzer;
        Stage = stage;
    }

    public CaptureId CaptureId { get; }

    public AnalysisPurpose Purpose { get; }

    public ProcessingBoundary ProcessingBoundary { get; }

    public AnalyzerIdentity? Analyzer { get; }

    public CaptureAnalysisAuthorizationStage Stage { get; }
}

public sealed record CaptureAnalysisAuthorizationDecision
{
    private CaptureAnalysisAuthorizationDecision(
        CaptureAnalysisAuthorizationRequest request,
        bool isAuthorized,
        CaptureAnalysisPolicyDenialReason denialReason,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        AnalysisProcessingPolicy? processingPolicy)
    {
        Request = request;
        IsAuthorized = isAuthorized;
        DenialReason = denialReason;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        TombstoneGeneration = tombstoneGeneration;
        ProcessingPolicy = processingPolicy;
    }

    public CaptureAnalysisAuthorizationRequest Request { get; }

    public bool IsAuthorized { get; }

    public CaptureAnalysisPolicyDenialReason DenialReason { get; }

    public long PolicyRevision { get; }

    public long ControlGeneration { get; }

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy { get; }

    public static CaptureAnalysisAuthorizationDecision Authorized(
        CaptureAnalysisAuthorizationRequest request,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        AnalysisProcessingPolicy processingPolicy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(processingPolicy);
        EnsureRevisions(policyRevision, controlGeneration, enrollmentGeneration, tombstoneGeneration);
        if (policyRevision == 0 || enrollmentGeneration == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Authorized decisions require positive policy and enrollment revisions.");
        }


        bool boundaryAuthorized = processingPolicy.AuthorizedPurpose == request.Purpose &&
            processingPolicy.AllowedBoundaries.Contains(request.ProcessingBoundary);
        bool providerAuthorized = request.Analyzer == null ||
            processingPolicy.IsEligible(
                request.Analyzer,
                request.ProcessingBoundary,
                request.Purpose);
        if (!boundaryAuthorized || !providerAuthorized)
        {
            throw new ArgumentException(
                "The processing policy does not authorize the requested operation.",
                nameof(processingPolicy));
        }

        return new(
            request,
            true,
            CaptureAnalysisPolicyDenialReason.None,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            processingPolicy);
    }

    public static CaptureAnalysisAuthorizationDecision Denied(
        CaptureAnalysisAuthorizationRequest request,
        CaptureAnalysisPolicyDenialReason reason,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        AnalysisProcessingPolicy? processingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(reason) || reason is
            CaptureAnalysisPolicyDenialReason.Unknown or CaptureAnalysisPolicyDenialReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        EnsureRevisions(policyRevision, controlGeneration, enrollmentGeneration, tombstoneGeneration);
        return new(
            request,
            false,
            reason,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            processingPolicy);
    }

    private static void EnsureRevisions(
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration)
    {
        if (policyRevision < 0 || controlGeneration < 0 ||
            enrollmentGeneration < 0 || tombstoneGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Policy and enrollment revisions cannot be negative.");
        }
    }
}

public sealed record CaptureAnalysisPolicySnapshot
{
    public CaptureAnalysisPolicySnapshot(
        CaptureAnalysisProcessingState processingState,
        bool isFutureCaptureAdmissionEnabled,
        long policyRevision,
        long controlGeneration,
        AnalysisPurpose? authorizedPurpose,
        AnalysisProcessingPolicy? processingPolicy,
        long futureCaptureSequenceWatermark,
        CaptureAnalysisBackfillState backfillState)
    {
        if (!Enum.IsDefined(processingState) || processingState == CaptureAnalysisProcessingState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingState));
        }

        if (policyRevision < 0 || controlGeneration < 0 || futureCaptureSequenceWatermark < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policyRevision));
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
            throw new ArgumentException("Enabled processing requires consistent purpose authorization.");
        }

        if (processingState == CaptureAnalysisProcessingState.Enabled && policyRevision == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Enabled processing requires a positive policy revision.");
        }

        if (processingState == CaptureAnalysisProcessingState.Disabled &&
            (hasAuthorization || isFutureCaptureAdmissionEnabled ||
             backfillState != CaptureAnalysisBackfillState.NotAuthorized))
        {
            throw new ArgumentException("Disabled processing must fail closed.");
        }

        ProcessingState = processingState;
        IsFutureCaptureAdmissionEnabled = isFutureCaptureAdmissionEnabled;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        AuthorizedPurpose = authorizedPurpose;
        ProcessingPolicy = processingPolicy;
        FutureCaptureSequenceWatermark = futureCaptureSequenceWatermark;
        BackfillState = backfillState;
    }

    public CaptureAnalysisProcessingState ProcessingState { get; }

    public bool IsFutureCaptureAdmissionEnabled { get; }

    public long PolicyRevision { get; }

    public long ControlGeneration { get; }

    public AnalysisPurpose? AuthorizedPurpose { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy { get; }

    public long FutureCaptureSequenceWatermark { get; }

    public CaptureAnalysisBackfillState BackfillState { get; }

    public bool IsProcessingAuthorized => ProcessingState == CaptureAnalysisProcessingState.Enabled;
}

public enum CaptureAnalysisPolicyChangeKind
{
    Unknown,
    EnableFutureCaptures,
    StopFutureCaptures,
    RevokeAndErase,
}

public sealed record CaptureAnalysisPolicyChange
{
    private CaptureAnalysisPolicyChange(
        CaptureAnalysisPolicyChangeKind kind,
        AnalysisPurpose? purpose,
        AnalysisProcessingPolicy? processingPolicy,
        long currentAssetSequence,
        bool authorizeExistingCaptureBackfill)
    {
        if (currentAssetSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentAssetSequence));
        }

        Kind = kind;
        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
        CurrentAssetSequence = currentAssetSequence;
        AuthorizeExistingCaptureBackfill = authorizeExistingCaptureBackfill;
    }

    public CaptureAnalysisPolicyChangeKind Kind { get; }

    public AnalysisPurpose? Purpose { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy { get; }

    public long CurrentAssetSequence { get; }

    public bool AuthorizeExistingCaptureBackfill { get; }

    public static CaptureAnalysisPolicyChange EnableFutureCaptures(
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy,
        long currentAssetSequence,
        bool authorizeExistingCaptureBackfill)
    {
        ArgumentNullException.ThrowIfNull(processingPolicy);
        if (purpose.IsEmpty || processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException("The processing policy must authorize the requested purpose.");
        }

        return new(
            CaptureAnalysisPolicyChangeKind.EnableFutureCaptures,
            purpose,
            processingPolicy,
            currentAssetSequence,
            authorizeExistingCaptureBackfill);
    }

    public static CaptureAnalysisPolicyChange StopFutureCaptures(long currentAssetSequence)
    {
        return new(CaptureAnalysisPolicyChangeKind.StopFutureCaptures, null, null, currentAssetSequence, false);
    }

    public static CaptureAnalysisPolicyChange RevokeAndErase(long currentAssetSequence)
    {
        return new(CaptureAnalysisPolicyChangeKind.RevokeAndErase, null, null, currentAssetSequence, false);
    }
}

public enum CaptureAnalysisPolicyChangeStatus
{
    Unknown,
    Succeeded,
    Conflict,
    Rejected,
    Unavailable,
}

public sealed record CaptureAnalysisPolicyChangeResult
{
    public CaptureAnalysisPolicyChangeResult(
        CaptureAnalysisPolicyChangeStatus status,
        CaptureAnalysisPolicySnapshot policy)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisPolicyChangeStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentNullException.ThrowIfNull(policy);
        Status = status;
        Policy = policy;
    }

    public CaptureAnalysisPolicyChangeStatus Status { get; }

    public CaptureAnalysisPolicySnapshot Policy { get; }
}

public interface ICaptureAnalysisPolicyService
{
    ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
        CaptureAnalysisAuthorizationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisPolicyChangeResult> TryApplyAsync(
        CaptureAnalysisPolicyChange change,
        long expectedPolicyRevision,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalysisFeatureAvailability
{
    bool IsCaptureAnalysisEnabled { get; }

    long ResolutionPolicyRevision { get; }

    bool IsProviderEnabled(string providerId);

    bool IsAnalyzerEnabled(AnalyzerIdentity analyzer);
}
