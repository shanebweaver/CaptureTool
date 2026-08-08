using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Analysis.Maintenance;

namespace CaptureTool.Application.Analysis.Policy;

/// <summary>
/// Coordinates policy commands whose durable state transition must be followed by cleanup.
/// Keeping this orchestration outside <see cref="CaptureAnalysisPolicyService"/> prevents the
/// policy query boundary from depending on cleanup, which itself uses policy-authorized
/// persistence services.
/// </summary>
internal sealed class CaptureAnalysisPolicyCommandService : ICaptureAnalysisPolicyCommandService
{
    private readonly CaptureAnalysisPolicyService _policyService;
    private readonly ICaptureAnalysisCleanupCoordinator _cleanup;

    public CaptureAnalysisPolicyCommandService(
        CaptureAnalysisPolicyService policyService,
        ICaptureAnalysisCleanupCoordinator cleanup)
    {
        _policyService = policyService;
        _cleanup = cleanup;
    }

    public async ValueTask<CaptureAnalysisPolicyChangeResult> ApplyConsentDecisionAsync(
        CaptureAnalysisConsentResponse response,
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        CaptureAnalysisPolicyChangeResult result = await _policyService.ApplyConsentDecisionAsync(
            response,
            expectedControlDocumentRevision,
            cancellationToken).ConfigureAwait(false);
        return response.Decision == CaptureAnalysisConsentDecision.Declined
            ? await ReconcileAuthorizationRemovalAsync(result).ConfigureAwait(false)
            : result;
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> ResumeFutureCaptureAdmissionAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return _policyService.ResumeFutureCaptureAdmissionAsync(
            expectedControlDocumentRevision,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> AuthorizeExistingCaptureBackfillAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return _policyService.AuthorizeExistingCaptureBackfillAsync(
            expectedControlDocumentRevision,
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> StopFutureCapturesAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        return _policyService.StopFutureCapturesAsync(
            expectedControlDocumentRevision,
            cancellationToken);
    }

    public async ValueTask<CaptureAnalysisPolicyChangeResult> RevokeAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        CaptureAnalysisPolicyChangeResult result = await _policyService.RevokeAsync(
            expectedControlDocumentRevision,
            cancellationToken).ConfigureAwait(false);
        return await ReconcileAuthorizationRemovalAsync(result).ConfigureAwait(false);
    }

    private async ValueTask<CaptureAnalysisPolicyChangeResult> ReconcileAuthorizationRemovalAsync(
        CaptureAnalysisPolicyChangeResult result)
    {
        if (result.Status == CaptureAnalysisPolicyChangeStatus.Succeeded)
        {
            // The authorization removal is already durable. Cleanup is idempotent and retried
            // during startup reconciliation, so caller cancellation must not interrupt it.
            _ = await _cleanup.ReconcileAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return result;
    }
}
