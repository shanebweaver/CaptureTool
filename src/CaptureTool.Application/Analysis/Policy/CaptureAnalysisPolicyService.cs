using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Policy;

internal sealed class CaptureAnalysisPolicyService :
    ICaptureAnalysisPolicyService,
    ICaptureAnalysisPolicyCommandService,
    IDisposable
{
    private readonly ICaptureAssetCatalog _captureAssetCatalog;
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    public CaptureAnalysisPolicyService(
        ICaptureAssetCatalog captureAssetCatalog,
        ICaptureAnalysisControlStore controlStore,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ISettingsService settingsService)
    {
        _captureAssetCatalog = captureAssetCatalog;
        _controlStore = controlStore;
        _featureAvailability = featureAvailability;
        _settingsService = settingsService;
    }

    public async ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        CaptureAnalysisConsentState settingsConsent = GetSettingsConsentState();
        if (!_featureAvailability.IsCaptureAnalysisEnabled)
        {
            CaptureAnalysisControlSnapshot? disabledControl = null;
            try
            {
                // The kill switch prevents all Analysis work, but settings still need the
                // content-free control revision so the user can revoke a prior grant.
                disabledControl = await _controlStore.GetAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // FeatureDisabled remains the safe authorization result even if management
                // state is temporarily unavailable.
            }

            return new(
                CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                settingsConsent,
                disabledControl);
        }

        try
        {
            CaptureAnalysisControlSnapshot control = await _controlStore.GetAsync(cancellationToken);
            return CreateSnapshot(settingsConsent, control);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(
                CaptureAnalysisPolicySnapshotStatus.Unavailable,
                settingsConsent);
        }
    }

    public async ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
        CaptureAnalysisAdmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureAnalysisPolicySnapshot snapshot = await GetCurrentAsync(cancellationToken);
        CaptureAnalysisPolicyDenialReason? snapshotDenial = GetSnapshotDenial(snapshot);
        if (snapshotDenial.HasValue)
        {
            return CaptureAnalysisAdmissionDecision.Denied(request, snapshotDenial.Value);
        }

        CaptureAnalysisControlState state = snapshot.ControlSnapshot!.State;
        CaptureAnalysisPolicy policy = state.Policy;
        if (request.Purpose != policy.AuthorizedPurpose)
        {
            return DenyAdmission(request, CaptureAnalysisPolicyDenialReason.PurposeNotAuthorized, state);
        }

        CaptureAnalysisEnrollment? enrollment = state.Enrollments.FirstOrDefault(
            item => item.CaptureId == request.CaptureId);
        CaptureAnalysisPolicyDenialReason? enrollmentDenial = GetEnrollmentDenial(enrollment);
        if (request.IsPrivateCapture || enrollment?.ExclusionReason == CaptureAnalysisExclusionReason.PrivateCapture)
        {
            return DenyAdmission(
                request,
                CaptureAnalysisPolicyDenialReason.PrivateCapture,
                state,
                enrollment);
        }

        if (enrollmentDenial.HasValue)
        {
            return DenyAdmission(request, enrollmentDenial.Value, state, enrollment);
        }

        if (enrollment?.State == CaptureAnalysisEnrollmentState.Enrolled)
        {
            return CaptureAnalysisAdmissionDecision.Authorized(
                request,
                state.PolicyRevision,
                state.ControlGeneration,
                enrollment.EnrollmentGeneration,
                enrollment.TombstoneGeneration,
                state.AuthorizationScope!);
        }

        bool isEligible = request.Kind switch
        {
            CaptureAnalysisAdmissionKind.FutureCapture =>
                policy.IsFutureCaptureEligible(request.AssetFinalizationSequence),
            CaptureAnalysisAdmissionKind.ExistingCaptureBackfill =>
                policy.IsExistingCaptureBackfillEligible(request.AssetFinalizationSequence),
            _ => false,
        };
        if (!isEligible)
        {
            CaptureAnalysisPolicyDenialReason reason =
                request.Kind == CaptureAnalysisAdmissionKind.ExistingCaptureBackfill
                    ? CaptureAnalysisPolicyDenialReason.BackfillNotAuthorized
                    : CaptureAnalysisPolicyDenialReason.CaptureBeforeFutureWatermark;
            return DenyAdmission(request, reason, state);
        }

        return CaptureAnalysisAdmissionDecision.Authorized(
            request,
            state.PolicyRevision,
            state.ControlGeneration,
            0,
            0,
            state.AuthorizationScope!);
    }

    public async ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
        CaptureAnalysisAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureAnalysisPolicySnapshot snapshot = await GetCurrentAsync(cancellationToken);
        CaptureAnalysisPolicyDenialReason? snapshotDenial = GetSnapshotDenial(snapshot);
        if (snapshotDenial.HasValue)
        {
            return CaptureAnalysisAuthorizationDecision.Denied(request, snapshotDenial.Value);
        }

        CaptureAnalysisControlState state = snapshot.ControlSnapshot!.State;
        if (request.Purpose != state.AuthorizedPurpose)
        {
            return DenyAuthorization(request, CaptureAnalysisPolicyDenialReason.PurposeNotAuthorized, state);
        }

        CaptureAnalysisEnrollment? enrollment = state.Enrollments.FirstOrDefault(
            item => item.CaptureId == request.CaptureId);
        CaptureAnalysisPolicyDenialReason? enrollmentDenial = GetEnrollmentDenial(enrollment);
        if (enrollmentDenial.HasValue || enrollment?.State != CaptureAnalysisEnrollmentState.Enrolled)
        {
            return DenyAuthorization(
                request,
                enrollmentDenial ?? CaptureAnalysisPolicyDenialReason.CaptureNotEnrolled,
                state,
                enrollment);
        }

        CaptureAnalysisAuthorizationScope authorizationScope = state.AuthorizationScope!;
        if (!authorizationScope.Allows(request.Capability))
        {
            return DenyAuthorization(
                request,
                CaptureAnalysisPolicyDenialReason.CapabilityNotAuthorized,
                state,
                enrollment);
        }

        AnalysisProcessingPolicy processingPolicy = authorizationScope.ProcessingPolicy;
        if (!processingPolicy.AllowedBoundaries.Contains(request.ProcessingBoundary))
        {
            return DenyAuthorization(
                request,
                CaptureAnalysisPolicyDenialReason.BoundaryNotAuthorized,
                state,
                enrollment);
        }

        if (request.Analyzer != null &&
            (!_featureAvailability.IsProviderEnabled(request.Analyzer.ProviderId) ||
             !_featureAvailability.IsAnalyzerEnabled(request.Analyzer) ||
             !processingPolicy.IsEligible(
                 request.Analyzer,
                 request.ProcessingBoundary,
                 request.Purpose)))
        {
            return DenyAuthorization(
                request,
                CaptureAnalysisPolicyDenialReason.ProviderNotAuthorized,
                state,
                enrollment);
        }

        return CaptureAnalysisAuthorizationDecision.Authorized(
            request,
            state.PolicyRevision,
            state.ControlGeneration,
            enrollment.EnrollmentGeneration,
            enrollment.TombstoneGeneration,
            authorizationScope);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> ApplyConsentDecisionAsync(
        CaptureAnalysisConsentResponse response,
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        PolicyMutation mutation = response.Decision switch
        {
            CaptureAnalysisConsentDecision.Cancelled => PolicyMutation.CancelConsent,
            CaptureAnalysisConsentDecision.Declined => PolicyMutation.DeclineConsent,
            CaptureAnalysisConsentDecision.GrantedForFutureCaptures =>
                PolicyMutation.GrantFutureCaptureConsent,
            _ => throw new ArgumentOutOfRangeException(nameof(response)),
        };

        return MutateAsync(
            mutation,
            expectedControlDocumentRevision,
            response,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> ResumeFutureCaptureAdmissionAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return MutateAsync(
            PolicyMutation.ResumeFutureCaptureAdmission,
            expectedControlDocumentRevision,
            null,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> AuthorizeExistingCaptureBackfillAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return MutateAsync(
            PolicyMutation.AuthorizeExistingCaptureBackfill,
            expectedControlDocumentRevision,
            null,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> StopFutureCapturesAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return MutateAsync(
            PolicyMutation.StopFutureCaptures,
            expectedControlDocumentRevision,
            null,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> RevokeAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return MutateAsync(
            PolicyMutation.Revoke,
            expectedControlDocumentRevision,
            null,
            cancellationToken);
    }

    private async ValueTask<CaptureAnalysisPolicyChangeResult> MutateAsync(
        PolicyMutation mutation,
        long expectedControlDocumentRevision,
        CaptureAnalysisConsentResponse? consentResponse,
        CancellationToken cancellationToken)
    {
        if (expectedControlDocumentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedControlDocumentRevision));
        }

        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            CaptureAnalysisConsentState settingsConsent = GetSettingsConsentState();
            if (!IsAuthorizationRemovingMutation(mutation) &&
                !_featureAvailability.IsCaptureAnalysisEnabled)
            {
                return new(
                    CaptureAnalysisPolicyChangeStatus.Rejected,
                    new CaptureAnalysisPolicySnapshot(
                        CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                        settingsConsent));
            }

            CaptureAnalysisControlSnapshot current;
            try
            {
                current = await _controlStore.GetAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new(
                    CaptureAnalysisPolicyChangeStatus.Unavailable,
                    new CaptureAnalysisPolicySnapshot(
                        CaptureAnalysisPolicySnapshotStatus.Unavailable,
                        settingsConsent));
            }

            CaptureAnalysisPolicySnapshot currentSnapshot = CreateSnapshot(settingsConsent, current);
            if (current.DocumentRevision != expectedControlDocumentRevision)
            {
                return new(CaptureAnalysisPolicyChangeStatus.Conflict, currentSnapshot);
            }

            if (mutation == PolicyMutation.CancelConsent)
            {
                return new(CaptureAnalysisPolicyChangeStatus.Rejected, currentSnapshot);
            }

            if (mutation == PolicyMutation.GrantFutureCaptureConsent &&
                !IsCurrentConsentDisclosure(consentResponse!.Disclosure))
            {
                return new(CaptureAnalysisPolicyChangeStatus.Rejected, currentSnapshot);
            }

            if ((mutation is PolicyMutation.AuthorizeExistingCaptureBackfill or
                PolicyMutation.StopFutureCaptures or
                PolicyMutation.ResumeFutureCaptureAdmission) &&
                !currentSnapshot.IsProcessingAuthorized)
            {
                return new(CaptureAnalysisPolicyChangeStatus.Rejected, currentSnapshot);
            }

            long? currentAssetSequence = null;
            if (!IsAuthorizationRemovingMutation(mutation))
            {
                try
                {
                    currentAssetSequence = _captureAssetCatalog.GetLatestChangeSequence();
                }
                catch
                {
                    return new(CaptureAnalysisPolicyChangeStatus.Unavailable, currentSnapshot);
                }
            }

            CaptureAnalysisPolicy nextPolicy = CreateNextPolicy(
                mutation,
                current.State.Policy,
                currentAssetSequence,
                consentResponse);
            var nextState = new CaptureAnalysisControlState(
                nextPolicy,
                RetainEnrollmentsForPolicyTransition(current.State, nextPolicy),
                current.State.CaptureChangeCheckpoint);

            CaptureAnalysisControlWriteResult writeResult;
            try
            {
                writeResult = await _controlStore.TryWriteAsync(
                    nextState,
                    current.DocumentRevision,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                CaptureAnalysisPolicySnapshot safeSnapshot = CreateSnapshot(settingsConsent, current);
                return new(CaptureAnalysisPolicyChangeStatus.Unavailable, safeSnapshot);
            }

            if (writeResult.Status != CaptureAnalysisControlWriteStatus.Succeeded)
            {
                CaptureAnalysisControlSnapshot safeControl = writeResult.Status ==
                    CaptureAnalysisControlWriteStatus.Conflict
                    ? writeResult.Snapshot!
                    : writeResult.Snapshot ?? current;
                CaptureAnalysisPolicySnapshot safeSnapshot = CreateSnapshot(settingsConsent, safeControl);
                CaptureAnalysisPolicyChangeStatus status = writeResult.Status == CaptureAnalysisControlWriteStatus.Conflict
                    ? CaptureAnalysisPolicyChangeStatus.Conflict
                    : CaptureAnalysisPolicyChangeStatus.Unavailable;
                return new(status, safeSnapshot);
            }

            CaptureAnalysisControlSnapshot committed = writeResult.Snapshot!;
            string? consentSettingValue = mutation switch
            {
                PolicyMutation.GrantFutureCaptureConsent =>
                    CaptureAnalysisConsentSettingValues.Granted,
                _ when IsAuthorizationRemovingMutation(mutation) =>
                    CaptureAnalysisConsentSettingValues.Denied,
                _ => null,
            };
            if (consentSettingValue != null)
            {
                SettingsMutationResult settingsResult;
                try
                {
                    // The authoritative control transition is already durable. Finish or report
                    // reconciliation; caller cancellation must not strand the fail-closed latch.
                    settingsResult = await _settingsService.TrySetAndSaveAsync(
                        CaptureToolSettings.Settings_CaptureAnalysisConsent,
                        consentSettingValue,
                        CancellationToken.None);
                }
                catch
                {
                    return new(
                        CaptureAnalysisPolicyChangeStatus.ReconciliationRequired,
                        CreateSnapshot(settingsConsent, committed));
                }

                if (!settingsResult.Succeeded)
                {
                    return new(
                        CaptureAnalysisPolicyChangeStatus.ReconciliationRequired,
                        CreateSnapshot(settingsConsent, committed));
                }

                settingsConsent = CaptureAnalysisConsentSettingValues.Parse(consentSettingValue);
            }

            return new(
                CaptureAnalysisPolicyChangeStatus.Succeeded,
                CreateSnapshot(settingsConsent, committed));
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private static CaptureAnalysisPolicy CreateNextPolicy(
        PolicyMutation mutation,
        CaptureAnalysisPolicy current,
        long? currentAssetSequence,
        CaptureAnalysisConsentResponse? consentResponse)
    {
        return mutation switch
        {
            PolicyMutation.GrantFutureCaptureConsent => current.GrantFutureCaptures(
                consentResponse!.Disclosure.AuthorizationScope,
                RequireAssetSequence(currentAssetSequence)),
            PolicyMutation.ResumeFutureCaptureAdmission => current.ResumeFutureCaptures(
                RequireAssetSequence(currentAssetSequence)),
            PolicyMutation.AuthorizeExistingCaptureBackfill =>
                current.AuthorizeExistingCaptureBackfill(RequireAssetSequence(currentAssetSequence)),
            PolicyMutation.StopFutureCaptures =>
                current.StopFutureCaptures(RequireAssetSequence(currentAssetSequence)),
            PolicyMutation.DeclineConsent or PolicyMutation.Revoke =>
                current.Revoke(),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
    }

    private static long RequireAssetSequence(long? currentAssetSequence)
    {
        return currentAssetSequence ?? throw new InvalidOperationException(
            "This policy transition requires a current Capture Asset sequence.");
    }

    private static bool IsAuthorizationRemovingMutation(PolicyMutation mutation)
    {
        return mutation is PolicyMutation.DeclineConsent or PolicyMutation.Revoke;
    }

    private static bool IsCurrentConsentDisclosure(CaptureAnalysisConsentDisclosure disclosure)
    {
        return disclosure.IsEquivalentTo(CaptureAnalysisPolicyDefaults.CreateConsentDisclosure());
    }

    private static IEnumerable<CaptureAnalysisEnrollment> RetainEnrollmentsForPolicyTransition(
        CaptureAnalysisControlState current,
        CaptureAnalysisPolicy nextPolicy)
    {
        bool authorizationEpochChanged =
            nextPolicy.PolicyRevision != current.PolicyRevision ||
            nextPolicy.ControlGeneration != current.ControlGeneration;
        if (!authorizationEpochChanged)
        {
            return current.Enrollments;
        }

        // An enrolled row is evidence for one authorization epoch only. Revocation or an
        // explicit renewal must retire it so a later grant cannot resurrect old captures.
        // Exclusions and forgotten-capture tombstones remain durable negative controls.
        return current.Enrollments.Where(
            enrollment => enrollment.State != CaptureAnalysisEnrollmentState.Enrolled);
    }

    private CaptureAnalysisPolicySnapshot CreateSnapshot(
        CaptureAnalysisConsentState settingsConsent,
        CaptureAnalysisControlSnapshot control)
    {
        if (!_featureAvailability.IsCaptureAnalysisEnabled)
        {
            return new(
                CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                settingsConsent,
                control);
        }

        CaptureAnalysisPolicy policy = control.State.Policy;
        if (settingsConsent != policy.ConsentState)
        {
            return new(
                CaptureAnalysisPolicySnapshotStatus.ConsentMismatch,
                settingsConsent,
                control);
        }

        if (policy.IsProcessingAuthorized && !IsCurrentMvpPolicy(policy))
        {
            return new(
                CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired,
                settingsConsent,
                control);
        }

        return new(
            CaptureAnalysisPolicySnapshotStatus.Available,
            settingsConsent,
            control);
    }

    private static bool IsCurrentMvpPolicy(CaptureAnalysisPolicy policy)
    {
        return policy.AuthorizationScope?.IsEquivalentTo(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope()) == true;
    }

    private CaptureAnalysisConsentState GetSettingsConsentState()
    {
        try
        {
            if (!_settingsService.IsSet(CaptureToolSettings.Settings_CaptureAnalysisConsent))
            {
                return CaptureAnalysisConsentState.Unknown;
            }

            return CaptureAnalysisConsentSettingValues.Parse(
                _settingsService.Get(CaptureToolSettings.Settings_CaptureAnalysisConsent));
        }
        catch
        {
            return CaptureAnalysisConsentState.Unknown;
        }
    }

    private static CaptureAnalysisPolicyDenialReason? GetSnapshotDenial(
        CaptureAnalysisPolicySnapshot snapshot)
    {
        if (snapshot.IsProcessingAuthorized)
        {
            return null;
        }

        return snapshot.Status switch
        {
            CaptureAnalysisPolicySnapshotStatus.FeatureDisabled =>
                CaptureAnalysisPolicyDenialReason.FeatureDisabled,
            CaptureAnalysisPolicySnapshotStatus.Unavailable =>
                CaptureAnalysisPolicyDenialReason.PolicyUnavailable,
            CaptureAnalysisPolicySnapshotStatus.ConsentMismatch =>
                CaptureAnalysisPolicyDenialReason.ConsentMismatch,
            CaptureAnalysisPolicySnapshotStatus.ConsentReviewRequired =>
                CaptureAnalysisPolicyDenialReason.ConsentReviewRequired,
            _ when snapshot.SettingsConsentState == CaptureAnalysisConsentState.Denied =>
                CaptureAnalysisPolicyDenialReason.ConsentDenied,
            _ => CaptureAnalysisPolicyDenialReason.ConsentUnknown,
        };
    }

    private static CaptureAnalysisPolicyDenialReason? GetEnrollmentDenial(
        CaptureAnalysisEnrollment? enrollment)
    {
        return enrollment?.State switch
        {
            CaptureAnalysisEnrollmentState.Excluded =>
                enrollment.ExclusionReason == CaptureAnalysisExclusionReason.PrivateCapture
                    ? CaptureAnalysisPolicyDenialReason.PrivateCapture
                    : CaptureAnalysisPolicyDenialReason.CaptureExcluded,
            CaptureAnalysisEnrollmentState.Forgotten =>
                CaptureAnalysisPolicyDenialReason.CaptureForgotten,
            _ => null,
        };
    }

    private static CaptureAnalysisAdmissionDecision DenyAdmission(
        CaptureAnalysisAdmissionRequest request,
        CaptureAnalysisPolicyDenialReason reason,
        CaptureAnalysisControlState state,
        CaptureAnalysisEnrollment? enrollment = null)
    {
        return CaptureAnalysisAdmissionDecision.Denied(
            request,
            reason,
            state.PolicyRevision,
            state.ControlGeneration,
            enrollment?.EnrollmentGeneration ?? 0,
            enrollment?.TombstoneGeneration ?? 0,
            state.AuthorizationScope);
    }

    private static CaptureAnalysisAuthorizationDecision DenyAuthorization(
        CaptureAnalysisAuthorizationRequest request,
        CaptureAnalysisPolicyDenialReason reason,
        CaptureAnalysisControlState state,
        CaptureAnalysisEnrollment? enrollment = null)
    {
        return CaptureAnalysisAuthorizationDecision.Denied(
            request,
            reason,
            state.PolicyRevision,
            state.ControlGeneration,
            enrollment?.EnrollmentGeneration ?? 0,
            enrollment?.TombstoneGeneration ?? 0,
            state.AuthorizationScope);
    }

    public void Dispose()
    {
        _mutationGate.Dispose();
    }

    private enum PolicyMutation
    {
        CancelConsent,
        DeclineConsent,
        GrantFutureCaptureConsent,
        ResumeFutureCaptureAdmission,
        AuthorizeExistingCaptureBackfill,
        StopFutureCaptures,
        Revoke,
    }
}
