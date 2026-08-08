using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Analysis.Policy;

public static class CaptureAnalysisPolicyDefaults
{
    private static readonly IReadOnlyList<CapabilityDefinition> CaptureMemorySearchCapabilitySet =
        Array.AsReadOnly(
        [
            AnalysisCapabilities.ImageDescriptionV1,
            AnalysisCapabilities.MediaPropertiesV1,
            AnalysisCapabilities.OcrDocumentV1,
        ]);

    public const string CaptureMemorySearchPurposeId = "capture-memory-search";

    public const int CaptureMemorySearchPurposeVersion = 1;

    public static AnalysisPurpose CaptureMemorySearchPurpose =>
        new(CaptureMemorySearchPurposeId, CaptureMemorySearchPurposeVersion);

    public static AnalysisProcessingPolicy CreateLocalOnlyPolicy() =>
        AnalysisProcessingPolicy.LocalOnly(CaptureMemorySearchPurpose);

    public static IReadOnlyList<CapabilityDefinition> CaptureMemorySearchCapabilities =>
        CaptureMemorySearchCapabilitySet;

    public static CaptureAnalysisAuthorizationScope CreateAuthorizationScope() =>
        new(
            CaptureMemorySearchPurpose,
            CreateLocalOnlyPolicy(),
            CaptureMemorySearchCapabilitySet);

    public static CaptureAnalysisConsentDisclosure CreateConsentDisclosure() =>
        new(CreateAuthorizationScope());
}

public enum CaptureAnalysisAuthorizationStage
{
    Unknown,
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
    PolicyUnavailable,
    ConsentUnknown,
    ConsentDenied,
    ConsentMismatch,
    ConsentReviewRequired,
    PurposeNotAuthorized,
    CaptureNotEnrolled,
    CaptureExcluded,
    PrivateCapture,
    CaptureForgotten,
    CaptureBeforeFutureWatermark,
    BackfillNotAuthorized,
    CapabilityNotAuthorized,
    BoundaryNotAuthorized,
    ProviderNotAuthorized,
    StalePolicy,
    StaleControlGeneration,
}

public enum CaptureAnalysisAdmissionKind
{
    Unknown,
    FutureCapture,
    ExistingCaptureBackfill,
}

public sealed record CaptureAnalysisAdmissionRequest
{
    public CaptureAnalysisAdmissionRequest(
        CaptureAssetChange finalization,
        AnalysisPurpose purpose,
        CaptureAnalysisAdmissionKind kind,
        bool isPrivateCapture = false)
    {
        if (finalization.CaptureId.IsEmpty ||
            finalization.Sequence <= 0 ||
            finalization.LifecycleRevision != 1 ||
            finalization.ChangeType != CaptureAssetChangeType.Finalized)
        {
            throw new ArgumentException(
                "Admission requires the original durable Capture Asset finalization fact.",
                nameof(finalization));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("Admission requires a purpose.", nameof(purpose));
        }

        if (!Enum.IsDefined(kind) || kind == CaptureAnalysisAdmissionKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Finalization = finalization;
        Purpose = purpose;
        Kind = kind;
        IsPrivateCapture = isPrivateCapture;
    }

    public CaptureAssetChange Finalization { get; }

    public CaptureId CaptureId => Finalization.CaptureId;

    // Always the original Finalized sequence, never a later source or preferred-location change.
    public long AssetFinalizationSequence => Finalization.Sequence;

    public AnalysisPurpose Purpose { get; }

    public CaptureAnalysisAdmissionKind Kind { get; }

    public bool IsPrivateCapture { get; }
}

public sealed record CaptureAnalysisAdmissionDecision
{
    private CaptureAnalysisAdmissionDecision(
        CaptureAnalysisAdmissionRequest request,
        bool isAuthorized,
        CaptureAnalysisPolicyDenialReason denialReason,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        CaptureAnalysisAuthorizationScope? authorizationScope)
    {
        Request = request;
        IsAuthorized = isAuthorized;
        DenialReason = denialReason;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        TombstoneGeneration = tombstoneGeneration;
        AuthorizationScope = authorizationScope;
    }

    public CaptureAnalysisAdmissionRequest Request { get; }

    public bool IsAuthorized { get; }

    public CaptureAnalysisPolicyDenialReason DenialReason { get; }

    public long PolicyRevision { get; }

    public long ControlGeneration { get; }

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    public CaptureAnalysisAuthorizationScope? AuthorizationScope { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy => AuthorizationScope?.ProcessingPolicy;

    public static CaptureAnalysisAdmissionDecision Authorized(
        CaptureAnalysisAdmissionRequest request,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        CaptureAnalysisAuthorizationScope authorizationScope)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        EnsureRevisions(policyRevision, controlGeneration, enrollmentGeneration, tombstoneGeneration);
        if (policyRevision == 0 || authorizationScope.Purpose != request.Purpose)
        {
            throw new ArgumentException("Authorized admission requires the exact current purpose and policy.");
        }

        return new(
            request,
            true,
            CaptureAnalysisPolicyDenialReason.None,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            authorizationScope);
    }

    public static CaptureAnalysisAdmissionDecision Denied(
        CaptureAnalysisAdmissionRequest request,
        CaptureAnalysisPolicyDenialReason reason,
        long policyRevision = 0,
        long controlGeneration = 0,
        long enrollmentGeneration = 0,
        long tombstoneGeneration = 0,
        CaptureAnalysisAuthorizationScope? authorizationScope = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureDenialReason(reason);
        EnsureRevisions(policyRevision, controlGeneration, enrollmentGeneration, tombstoneGeneration);
        return new(
            request,
            false,
            reason,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            authorizationScope);
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

    private static void EnsureDenialReason(CaptureAnalysisPolicyDenialReason reason)
    {
        if (!Enum.IsDefined(reason) || reason is
            CaptureAnalysisPolicyDenialReason.Unknown or CaptureAnalysisPolicyDenialReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
    }
}

public sealed record CaptureAnalysisAuthorizationRequest
{
    public CaptureAnalysisAuthorizationRequest(
        CaptureId captureId,
        AnalysisPurpose purpose,
        CapabilityDefinition capability,
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

        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException(
                "Authorization requires an exact capability definition.",
                nameof(capability));
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
        Capability = capability;
        ProcessingBoundary = processingBoundary;
        Analyzer = analyzer;
        Stage = stage;
    }

    public CaptureId CaptureId { get; }

    public AnalysisPurpose Purpose { get; }

    public CapabilityDefinition Capability { get; }

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
        CaptureAnalysisAuthorizationScope? authorizationScope)
    {
        Request = request;
        IsAuthorized = isAuthorized;
        DenialReason = denialReason;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        TombstoneGeneration = tombstoneGeneration;
        AuthorizationScope = authorizationScope;
    }

    public CaptureAnalysisAuthorizationRequest Request { get; }

    public bool IsAuthorized { get; }

    public CaptureAnalysisPolicyDenialReason DenialReason { get; }

    public long PolicyRevision { get; }

    public long ControlGeneration { get; }

    public long EnrollmentGeneration { get; }

    public long TombstoneGeneration { get; }

    public CaptureAnalysisAuthorizationScope? AuthorizationScope { get; }

    public AnalysisProcessingPolicy? ProcessingPolicy => AuthorizationScope?.ProcessingPolicy;

    public static CaptureAnalysisAuthorizationDecision Authorized(
        CaptureAnalysisAuthorizationRequest request,
        long policyRevision,
        long controlGeneration,
        long enrollmentGeneration,
        long tombstoneGeneration,
        CaptureAnalysisAuthorizationScope authorizationScope)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        EnsureRevisions(policyRevision, controlGeneration, enrollmentGeneration, tombstoneGeneration);
        if (policyRevision == 0 || enrollmentGeneration == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Authorized decisions require positive policy and enrollment revisions.");
        }

        AnalysisProcessingPolicy processingPolicy = authorizationScope.ProcessingPolicy;
        bool boundaryAuthorized = authorizationScope.Purpose == request.Purpose &&
            processingPolicy.AllowedBoundaries.Contains(request.ProcessingBoundary);
        bool providerAuthorized = request.Analyzer == null ||
            processingPolicy.IsEligible(
                request.Analyzer,
                request.ProcessingBoundary,
                request.Purpose);
        if (!authorizationScope.Allows(request.Capability) ||
            !boundaryAuthorized || !providerAuthorized)
        {
            throw new ArgumentException(
                "The processing policy does not authorize the requested operation.",
                nameof(authorizationScope));
        }

        return new(
            request,
            true,
            CaptureAnalysisPolicyDenialReason.None,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            authorizationScope);
    }

    public static CaptureAnalysisAuthorizationDecision Denied(
        CaptureAnalysisAuthorizationRequest request,
        CaptureAnalysisPolicyDenialReason reason,
        long policyRevision = 0,
        long controlGeneration = 0,
        long enrollmentGeneration = 0,
        long tombstoneGeneration = 0,
        CaptureAnalysisAuthorizationScope? authorizationScope = null)
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
            authorizationScope);
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

public enum CaptureAnalysisPolicySnapshotStatus
{
    Unknown,
    Available,
    FeatureDisabled,
    Unavailable,
    ConsentMismatch,
    ConsentReviewRequired,
}

public sealed record CaptureAnalysisPolicySnapshot
{
    public CaptureAnalysisPolicySnapshot(
        CaptureAnalysisPolicySnapshotStatus status,
        CaptureAnalysisConsentState settingsConsentState,
        CaptureAnalysisControlSnapshot? controlSnapshot = null)
    {
        if (!Enum.IsDefined(status) || status == CaptureAnalysisPolicySnapshotStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (!Enum.IsDefined(settingsConsentState))
        {
            throw new ArgumentOutOfRangeException(nameof(settingsConsentState));
        }

        if ((status is CaptureAnalysisPolicySnapshotStatus.Available or
            CaptureAnalysisPolicySnapshotStatus.ConsentMismatch or
            CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired) && controlSnapshot == null)
        {
            throw new ArgumentException("This policy status requires a durable control snapshot.", nameof(controlSnapshot));
        }

        Status = status;
        SettingsConsentState = settingsConsentState;
        ControlSnapshot = controlSnapshot;
    }

    public CaptureAnalysisPolicySnapshotStatus Status { get; }

    public CaptureAnalysisConsentState SettingsConsentState { get; }

    public CaptureAnalysisControlSnapshot? ControlSnapshot { get; }

    public long ControlDocumentRevision => ControlSnapshot?.DocumentRevision ?? 0;

    public CaptureAnalysisPolicy? Policy => ControlSnapshot?.State.Policy;

    public bool IsProcessingAuthorized =>
        Status == CaptureAnalysisPolicySnapshotStatus.Available &&
        SettingsConsentState == CaptureAnalysisConsentState.Granted &&
        Policy is { IsProcessingAuthorized: true, ConsentState: CaptureAnalysisConsentState.Granted };
}

public enum CaptureAnalysisPolicyChangeStatus
{
    Unknown,
    Succeeded,
    Conflict,
    Rejected,
    Unavailable,
    ReconciliationRequired,
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

    ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
        CaptureAnalysisAdmissionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
        CaptureAnalysisAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalysisPolicyCommandService
{
    ValueTask<CaptureAnalysisPolicyChangeResult> ApplyConsentDecisionAsync(
        CaptureAnalysisConsentResponse response,
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisPolicyChangeResult> ResumeFutureCaptureAdmissionAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisPolicyChangeResult> AuthorizeExistingCaptureBackfillAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisPolicyChangeResult> StopFutureCapturesAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default);

    ValueTask<CaptureAnalysisPolicyChangeResult> RevokeAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default);
}

public interface ICaptureAnalysisFeatureAvailability
{
    bool IsCaptureAnalysisEnabled { get; }

    long ResolutionPolicyRevision { get; }

    bool IsProviderEnabled(string providerId);

    bool IsAnalyzerEnabled(AnalyzerIdentity analyzer);
}
