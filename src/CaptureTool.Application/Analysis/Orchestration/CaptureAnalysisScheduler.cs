using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Orchestration;

internal sealed class CaptureAnalysisScheduler : ICaptureAnalysisScheduler
{
    private const int MaximumEnrollmentAttempts = 4;

    private readonly ICaptureAnalysisPolicyService _policyService;
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAnalysisSourceVerifier _sourceVerifier;
    private readonly ICaptureAnalysisMutationCoordinator _mutationCoordinator;
    private readonly ICaptureAnalysisStore _metadataStore;
    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisWakeSignal _wakeSignal;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ICaptureAnalyzerCatalog _analyzers;
    private readonly IClock _clock;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly ICaptureAnalysisCleanupCoordinator? _cleanup;
    private readonly CaptureAnalysisEnrollmentGate _enrollmentGate;

    public CaptureAnalysisScheduler(
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisControlStore controlStore,
        ICaptureAnalysisSourceVerifier sourceVerifier,
        ICaptureAnalysisMutationCoordinator mutationCoordinator,
        ICaptureAnalysisStore metadataStore,
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisWakeSignal wakeSignal,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ICaptureAnalyzerCatalog analyzers,
        ICaptureAssetCatalog captureAssets,
        IClock clock,
        ICaptureAnalysisCleanupCoordinator? cleanup = null,
        CaptureAnalysisEnrollmentGate? enrollmentGate = null)
    {
        _policyService = policyService;
        _controlStore = controlStore;
        _sourceVerifier = sourceVerifier;
        _mutationCoordinator = mutationCoordinator;
        _metadataStore = metadataStore;
        _jobStore = jobStore;
        _wakeSignal = wakeSignal;
        _featureAvailability = featureAvailability;
        _analyzers = analyzers;
        _captureAssets = captureAssets;
        _clock = clock;
        _cleanup = cleanup;
        _enrollmentGate = enrollmentGate ?? new();
    }

    public async ValueTask<CaptureAnalysisScheduleResult> ScheduleAsync(
        CaptureAnalysisScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Recipe.MediaKind == CaptureMediaKind.Unknown)
        {
            return new(CaptureAnalysisScheduleStatus.Unavailable);
        }

        CaptureAnalysisAdmissionDecision admission = await EnsureEnrollmentAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (!admission.IsAuthorized)
        {
            return new(CaptureAnalysisScheduleStatus.Denied, denialReason: admission.DenialReason);
        }

        if (admission.EnrollmentGeneration <= 0 || admission.ProcessingPolicy == null)
        {
            return new(CaptureAnalysisScheduleStatus.Conflict);
        }

        List<CaptureAnalysisAuthorizationDecision> sourceAuthorizations = [];
        foreach (RecipeCapability recipeCapability in request.Recipe.Capabilities)
        {
            AnalyzerIdentity? analyzer = GetBoundaryIdentity(
                recipeCapability.Capability,
                request.ProcessingBoundary);
            if (request.ProcessingBoundary == ProcessingBoundary.Remote && analyzer == null)
            {
                return new(CaptureAnalysisScheduleStatus.Unavailable);
            }

            var authorizationRequest = new CaptureAnalysisAuthorizationRequest(
                request.Admission.CaptureId,
                request.Admission.Purpose,
                recipeCapability.Capability,
                request.ProcessingBoundary,
                analyzer,
                CaptureAnalysisAuthorizationStage.SourceVerification);
            CaptureAnalysisAuthorizationDecision authorization = await _policyService
                .AuthorizeAsync(authorizationRequest, cancellationToken)
                .ConfigureAwait(false);
            if (!authorization.IsAuthorized)
            {
                return new(
                    CaptureAnalysisScheduleStatus.Denied,
                    denialReason: authorization.DenialReason);
            }

            if (!MatchesAdmission(admission, authorization))
            {
                return new(CaptureAnalysisScheduleStatus.Conflict);
            }

            sourceAuthorizations.Add(authorization);
        }

        IVerifiedCaptureAnalysisSource? source = await _sourceVerifier.TryOpenVerifiedAsync(
            new CaptureAnalysisSourceVerificationRequest(sourceAuthorizations[0]),
            cancellationToken).ConfigureAwait(false);
        if (source == null || source.MediaKind != request.Recipe.MediaKind)
        {
            if (source != null)
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }

            return new(CaptureAnalysisScheduleStatus.SourceUnavailable);
        }

        await using (source.ConfigureAwait(false))
        {
            CaptureAsset? asset = _captureAssets.Get(request.Admission.CaptureId);
            if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active })
            {
                return new(CaptureAnalysisScheduleStatus.SourceUnavailable);
            }

            var preconditions = new AnalysisCommitPreconditions(
                request.Admission.CaptureId,
                source.CaptureSourceGeneration,
                source.SourceStamp,
                source.SourceRevision,
                request.Admission.Purpose,
                admission.PolicyRevision,
                admission.ControlGeneration,
                admission.EnrollmentGeneration,
                admission.TombstoneGeneration,
                request.Recipe.Id,
                request.Recipe.Version,
                _featureAvailability.ResolutionPolicyRevision);
            CaptureAnalysisStoreSnapshot? existing = await _metadataStore
                .GetAsync(request.Admission.CaptureId, cancellationToken)
                .ConfigureAwait(false);
            var registration = new CaptureAnalysisSourceRegistration(
                preconditions,
                source.MediaKind,
                asset.CapturedAtUtc,
                request.Recipe);
            CaptureAnalysisStoreWriteResult registrationResult = await _mutationCoordinator
                .TryRegisterSourceAsync(
                    registration,
                    existing?.DocumentRevision,
                    cancellationToken).ConfigureAwait(false);
            if (registrationResult.Status != CaptureAnalysisStoreWriteStatus.Succeeded)
            {
                return new(registrationResult.Status switch
                {
                    CaptureAnalysisStoreWriteStatus.StaleCommit => CaptureAnalysisScheduleStatus.Conflict,
                    CaptureAnalysisStoreWriteStatus.Conflict => CaptureAnalysisScheduleStatus.Conflict,
                    _ => CaptureAnalysisScheduleStatus.Unavailable,
                });
            }

            int enqueued = 0;
            int alreadyExists = 0;
            DateTimeOffset enqueuedAtUtc = GetUtcNow();
            long executionOrder = 0;
            foreach (RecipeCapability capability in request.Recipe.GetExecutionOrder())
            {
                DateTimeOffset capabilityEnqueuedAtUtc = enqueuedAtUtc.AddTicks(executionOrder);
                executionOrder++;
                bool hasStaleProducer = HasStaleProducer(
                    existing,
                    capability.Capability,
                    request.ProcessingBoundary);
                bool hasMissingAnalysis = !registrationResult.Snapshot!.Record.TryGetAnalysis(
                    capability.Capability.Id,
                    out _);
                var key = new CaptureAnalysisJobKey(
                    preconditions,
                    capability.Capability,
                    request.ProcessingBoundary,
                    capability.Dependencies);
                CaptureAnalysisJobEnqueueResult enqueue = request.OperationId is Guid operationId
                    ? await _jobStore.TryScheduleOperationAsync(key, capabilityEnqueuedAtUtc, operationId, cancellationToken).ConfigureAwait(false)
                    : await _jobStore.TryEnqueueAsync(key, capabilityEnqueuedAtUtc, cancellationToken).ConfigureAwait(false);
                if (!request.OperationId.HasValue && enqueue.Status == CaptureAnalysisJobEnqueueStatus.AlreadyExists &&
                    (request.ForceReanalysis || hasStaleProducer || hasMissingAnalysis))
                {
                    enqueue = await _jobStore
                        .TryRequeueAsync(key, capabilityEnqueuedAtUtc, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (enqueue.Status == CaptureAnalysisJobEnqueueStatus.Enqueued)
                {
                    enqueued++;
                }
                else if (enqueue.Status == CaptureAnalysisJobEnqueueStatus.AlreadyExists)
                {
                    alreadyExists++;
                }
                else
                {
                    return new(enqueue.Status == CaptureAnalysisJobEnqueueStatus.Rejected
                        ? CaptureAnalysisScheduleStatus.Conflict
                        : CaptureAnalysisScheduleStatus.Unavailable);
                }
            }

            _ = _wakeSignal.TrySignal();
            int durableCount = enqueued + alreadyExists;
            return new(
                enqueued > 0
                    ? CaptureAnalysisScheduleStatus.Scheduled
                    : CaptureAnalysisScheduleStatus.AlreadyScheduled,
                durableCount);
        }
    }

    private async ValueTask<CaptureAnalysisAdmissionDecision> EnsureEnrollmentAsync(
        CaptureAnalysisScheduleRequest request,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumEnrollmentAttempts; attempt++)
        {
            CaptureAnalysisAdmissionDecision admission = await _policyService
                .AuthorizeAdmissionAsync(request.Admission, cancellationToken)
                .ConfigureAwait(false);
            if (!admission.IsAuthorized || admission.EnrollmentGeneration > 0)
            {
                if (admission.IsAuthorized && admission.EnrollmentGeneration > 0)
                {
                    CaptureAnalysisControlSnapshot enrolledControl = await _controlStore
                        .GetAsync(cancellationToken)
                        .ConfigureAwait(false);
                    CaptureAnalysisEnrollment? existing = enrolledControl.State.Enrollments
                        .FirstOrDefault(enrollment => enrollment.CaptureId == request.Admission.CaptureId);
                    if (existing?.RequestedRecipeId != request.Recipe.Id ||
                        existing.RequestedRecipeVersion != request.Recipe.Version)
                    {
                        return CaptureAnalysisAdmissionDecision.Denied(
                            request.Admission,
                            CaptureAnalysisPolicyDenialReason.StaleControlGeneration,
                            admission.PolicyRevision,
                            admission.ControlGeneration,
                            admission.EnrollmentGeneration,
                            admission.TombstoneGeneration,
                            admission.AuthorizationScope);
                    }
                }

                return admission;
            }

            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);
            CaptureAnalysisEnrollment? retired = current.State.Enrollments.FirstOrDefault(
                enrollment => enrollment.CaptureId == request.Admission.CaptureId);
            bool canRestoreCleared = retired?.IsMemoryCleared == true &&
                request.Admission.Kind == CaptureAnalysisAdmissionKind.ExistingCaptureBackfill &&
                retired.TombstoneGeneration == admission.TombstoneGeneration &&
                retired.AssetFinalizationSequence == request.Admission.AssetFinalizationSequence &&
                current.State.Policy.IsExistingCaptureBackfillEligible(
                    request.Admission.AssetFinalizationSequence);
            if (current.State.ControlGeneration != admission.ControlGeneration ||
                current.State.PolicyRevision != admission.PolicyRevision ||
                retired != null && !canRestoreCleared)
            {
                continue;
            }

            // Finish the old generation's purge before creating fresh work. The CAS below
            // rejects a concurrent exclusion/revoke, and generations fence off old workers.
            if (canRestoreCleared && (_cleanup == null ||
                !await _cleanup.ReconcileCaptureAsync(request.Admission.CaptureId, cancellationToken)
                    .ConfigureAwait(false)))
            {
                return CaptureAnalysisAdmissionDecision.Denied(
                    request.Admission, CaptureAnalysisPolicyDenialReason.PolicyUnavailable);
            }

            var enrollment = new CaptureAnalysisEnrollment(
                request.Admission.CaptureId,
                CaptureAnalysisEnrollmentState.Enrolled,
                CaptureAnalysisExclusionReason.None,
                enrollmentGeneration: checked((retired?.EnrollmentGeneration ?? 0) + 1),
                tombstoneGeneration: admission.TombstoneGeneration,
                request.Admission.AssetFinalizationSequence,
                request.Recipe.Id,
                request.Recipe.Version);
            var nextState = new CaptureAnalysisControlState(
                current.State.Policy,
                [.. current.State.Enrollments.Where(row => row.CaptureId != enrollment.CaptureId), enrollment],
                current.State.CaptureChangeCheckpoint);
            using IDisposable enrollmentLease = await _enrollmentGate.EnterAsync(cancellationToken).ConfigureAwait(false);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                nextState,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                continue;
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return CaptureAnalysisAdmissionDecision.Denied(
                    request.Admission,
                    CaptureAnalysisPolicyDenialReason.PolicyUnavailable);
            }
        }

        return CaptureAnalysisAdmissionDecision.Denied(
            request.Admission,
            CaptureAnalysisPolicyDenialReason.StaleControlGeneration);
    }

    private AnalyzerIdentity? GetBoundaryIdentity(
        CapabilityDefinition capability,
        ProcessingBoundary boundary)
    {
        return boundary == ProcessingBoundary.OnDevice
            ? null
            : _analyzers.Analyzers.FirstOrDefault(analyzer =>
                analyzer.Descriptor.Capability == capability &&
                analyzer.Descriptor.ProcessingBoundary == boundary)?.Descriptor.Identity;
    }

    private bool HasStaleProducer(
        CaptureAnalysisStoreSnapshot? existing,
        CapabilityDefinition capability,
        ProcessingBoundary processingBoundary)
    {
        if (existing == null ||
            !existing.Record.TryGetAnalysis(capability.Id, out CapabilityAnalysis? analysis) ||
            analysis == null)
        {
            return false;
        }

        AnalyzerRevision[] retainedRevisions =
        [
            .. new AnalyzerRevision?[]
            {
                analysis.CanonicalResult is { ProcessingBoundary: var resultBoundary } result &&
                    resultBoundary == processingBoundary
                        ? result.Analyzer.Revision
                        : null,
                analysis.LatestOutcome is { ProcessingBoundary: var outcomeBoundary } outcome &&
                    outcomeBoundary == processingBoundary
                        ? outcome.Analyzer.Revision
                        : null,
            }.Where(revision => revision.HasValue).Select(revision => revision!.Value),
        ];
        if (retainedRevisions.Length == 0)
        {
            return false;
        }

        AnalyzerRevision[] currentRevisions = _analyzers.Analyzers
            .Where(analyzer =>
                analyzer.Descriptor.Capability == capability &&
                analyzer.Descriptor.ProcessingBoundary == processingBoundary &&
                _featureAvailability.IsAnalyzerEnabled(analyzer.Descriptor.Identity))
            .Select(analyzer => analyzer.Descriptor.Revision)
            .Distinct()
            .ToArray();
        return retainedRevisions.Any(revision => !currentRevisions.Contains(revision));
    }

    private static bool MatchesAdmission(
        CaptureAnalysisAdmissionDecision admission,
        CaptureAnalysisAuthorizationDecision authorization)
    {
        return admission.PolicyRevision == authorization.PolicyRevision &&
            admission.ControlGeneration == authorization.ControlGeneration &&
            admission.EnrollmentGeneration == authorization.EnrollmentGeneration &&
            admission.TombstoneGeneration == authorization.TombstoneGeneration;
    }

    private DateTimeOffset GetUtcNow()
    {
        return new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
    }
}
