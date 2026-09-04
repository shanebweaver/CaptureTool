using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using System.Collections.Concurrent;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed class CaptureAnalysisMutationCoordinator : ICaptureAnalysisMutationCoordinator
{
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAnalysisPolicyService _policyService;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ICaptureAnalysisSourceVerifier _sourceVerifier;
    private readonly ICaptureAnalyzerCatalog _analyzers;
    private readonly LocalCaptureAnalysisStore _metadataStore;
    private readonly ConcurrentDictionary<CaptureId, SemaphoreSlim> _captureGates = new();

    public CaptureAnalysisMutationCoordinator(
        ICaptureAssetCatalog captureAssets,
        ICaptureAnalysisControlStore controlStore,
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ICaptureAnalysisSourceVerifier sourceVerifier,
        ICaptureAnalyzerCatalog analyzers,
        LocalCaptureAnalysisStore metadataStore)
    {
        _captureAssets = captureAssets;
        _controlStore = controlStore;
        _policyService = policyService;
        _featureAvailability = featureAvailability;
        _sourceVerifier = sourceVerifier;
        _analyzers = analyzers;
        _metadataStore = metadataStore;
    }

    public async ValueTask<CaptureAnalysisStoreWriteResult> TryRegisterSourceAsync(
        CaptureAnalysisSourceRegistration registration,
        long? expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        SemaphoreSlim gate = GetGate(registration.Preconditions.CaptureId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            VerifiedState? verified = await TryVerifyRegistrationAsync(registration, cancellationToken)
                .ConfigureAwait(false);
            if (verified == null)
            {
                return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
            }

            await using (verified.Source.ConfigureAwait(false))
            {
                CaptureAnalysisStoreSnapshot? current = await _metadataStore
                    .GetAsync(registration.Preconditions.CaptureId, cancellationToken)
                    .ConfigureAwait(false);
                if ((current == null) != !expectedDocumentRevision.HasValue ||
                    current != null && current.DocumentRevision != expectedDocumentRevision)
                {
                    return new(CaptureAnalysisStoreWriteStatus.Conflict, current);
                }

                CaptureAnalysisRecord record;
                if (current == null)
                {
                    record = new(
                        registration.Preconditions.CaptureId,
                        registration.MediaKind,
                        registration.CapturedAtUtc,
                        registration.Preconditions.SourceRevision,
                        registration.Recipe);
                }
                else
                {
                    record = current.Record;
                    bool sourceUnchanged = record.SourceRevision == registration.Preconditions.SourceRevision;
                    bool recipeUnchanged = record.Recipe.Version == registration.Recipe.Version &&
                        record.Recipe.HasSameSemanticsAs(registration.Recipe);
                    _ = record.RegisterSourceRevision(registration.Preconditions.SourceRevision);
                    _ = record.ApplyRecipe(registration.Recipe);
                    bool producersUnchanged = ReconcileProducerRevisions(
                        record,
                        registration.Capabilities,
                        verified.Boundary);
                    if (sourceUnchanged && recipeUnchanged && producersUnchanged)
                    {
                        return await IsStillAuthorizedAsync(
                            registration.Preconditions,
                            verified.Capability,
                            verified.Boundary,
                            verified.Analyzer,
                            CaptureAnalysisAuthorizationStage.SourceVerification,
                            cancellationToken).ConfigureAwait(false)
                                ? new(CaptureAnalysisStoreWriteStatus.Succeeded, current)
                                : new(CaptureAnalysisStoreWriteStatus.StaleCommit);
                    }

                }

                if (!await IsStillAuthorizedAsync(
                    registration.Preconditions,
                    verified.Capability,
                    verified.Boundary,
                    verified.Analyzer,
                    CaptureAnalysisAuthorizationStage.SourceVerification,
                    cancellationToken).ConfigureAwait(false))
                {
                    return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
                }

                return await _metadataStore
                    .TryWriteAsync(record, expectedDocumentRevision, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
        AnalysisCommitToken commitToken,
        CanonicalCapabilityResult result,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return TryCommitCapabilityCoreAsync(
            commitToken,
            result.ProcessingBoundary,
            expectedDocumentRevision,
            (record, current, analyzerRevision) => record.TryCommitResult(
                commitToken,
                current,
                analyzerRevision,
                result),
            cancellationToken);
    }

    public ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
        AnalysisCommitToken commitToken,
        CapabilityOutcome outcome,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return TryCommitCapabilityCoreAsync(
            commitToken,
            outcome.ProcessingBoundary,
            expectedDocumentRevision,
            (record, current, analyzerRevision) => record.TryRecordOutcome(
                commitToken,
                current,
                analyzerRevision,
                outcome),
            cancellationToken);
    }

    public async ValueTask<CaptureAnalysisStoreWriteResult> TryDeleteAsync(
        CaptureAnalysisDeletionToken deletionToken,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(deletionToken.CaptureId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CaptureAnalysisControlSnapshot control = await _controlStore
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);
            CaptureAnalysisEnrollment? enrollment = control.State.Enrollments.FirstOrDefault(
                candidate => candidate.CaptureId == deletionToken.CaptureId);
            if (control.State.ControlGeneration != deletionToken.ControlGeneration ||
                enrollment == null ||
                enrollment.State is not (CaptureAnalysisEnrollmentState.Excluded or
                    CaptureAnalysisEnrollmentState.Forgotten) ||
                enrollment.TombstoneGeneration != deletionToken.TombstoneGeneration)
            {
                return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
            }

            CaptureAnalysisStoreSnapshot? current = await _metadataStore
                .GetAsync(deletionToken.CaptureId, cancellationToken)
                .ConfigureAwait(false);
            if (current == null)
            {
                return new(CaptureAnalysisStoreWriteStatus.NotFound);
            }

            if (current.DocumentRevision != expectedDocumentRevision)
            {
                return new(CaptureAnalysisStoreWriteStatus.Conflict, current);
            }

            CaptureAnalysisControlSnapshot finalControl = await _controlStore
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);
            CaptureAnalysisEnrollment? finalEnrollment = finalControl.State.Enrollments.FirstOrDefault(
                candidate => candidate.CaptureId == deletionToken.CaptureId);
            if (finalControl.State.ControlGeneration != deletionToken.ControlGeneration ||
                finalEnrollment == null ||
                finalEnrollment.State is not (CaptureAnalysisEnrollmentState.Excluded or
                    CaptureAnalysisEnrollmentState.Forgotten) ||
                finalEnrollment.TombstoneGeneration != deletionToken.TombstoneGeneration)
            {
                return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
            }

            return await _metadataStore
                .TryDeleteAsync(deletionToken.CaptureId, expectedDocumentRevision, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityCoreAsync(
        AnalysisCommitToken commitToken,
        ProcessingBoundary processingBoundary,
        long expectedDocumentRevision,
        Func<CaptureAnalysisRecord, AnalysisCommitPreconditions, AnalyzerRevision, CapabilityCommitResult>
            applyCommit,
        CancellationToken cancellationToken)
    {
        if (commitToken.Expected.CaptureId.IsEmpty)
        {
            throw new ArgumentException("A capability commit requires a token.", nameof(commitToken));
        }

        if (expectedDocumentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDocumentRevision));
        }

        ICaptureAnalyzer? analyzer = _analyzers.Find(
            commitToken.AnalyzerRevision,
            commitToken.Capability);
        if (analyzer == null ||
            analyzer.Descriptor.Capability != commitToken.Capability ||
            analyzer.Descriptor.ProcessingBoundary != processingBoundary)
        {
            return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
        }

        SemaphoreSlim gate = GetGate(commitToken.CaptureId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            VerifiedState? verified = await TryAuthorizeAndVerifyAsync(
                commitToken.Expected,
                commitToken.Capability,
                analyzer.Descriptor.ProcessingBoundary,
                analyzer.Descriptor.Identity,
                CaptureAnalysisAuthorizationStage.CapabilityCommit,
                cancellationToken).ConfigureAwait(false);
            if (verified == null)
            {
                return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
            }

            await using (verified.Source.ConfigureAwait(false))
            {
                CaptureAnalysisStoreSnapshot? current = await _metadataStore
                    .GetAsync(commitToken.CaptureId, cancellationToken)
                    .ConfigureAwait(false);
                if (current == null)
                {
                    return new(CaptureAnalysisStoreWriteStatus.NotFound);
                }

                if (current.DocumentRevision != expectedDocumentRevision)
                {
                    return new(CaptureAnalysisStoreWriteStatus.Conflict, current);
                }

                if (!await IsStillAuthorizedAsync(
                    commitToken.Expected,
                    commitToken.Capability,
                    analyzer.Descriptor.ProcessingBoundary,
                    analyzer.Descriptor.Identity,
                    CaptureAnalysisAuthorizationStage.CapabilityCommit,
                    cancellationToken).ConfigureAwait(false))
                {
                    return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
                }

                CapabilityCommitResult commitResult = applyCommit(
                    current.Record,
                    commitToken.Expected,
                    analyzer.Descriptor.Revision);
                if (commitResult == CapabilityCommitResult.Stale)
                {
                    return new(CaptureAnalysisStoreWriteStatus.StaleCommit);
                }

                if (commitResult == CapabilityCommitResult.AlreadyCurrent)
                {
                    return new(CaptureAnalysisStoreWriteStatus.Succeeded, current);
                }

                return await _metadataStore
                    .TryWriteAsync(current.Record, expectedDocumentRevision, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<VerifiedState?> TryVerifyRegistrationAsync(
        CaptureAnalysisSourceRegistration registration,
        CancellationToken cancellationToken)
    {
        RecipeCapability capability = registration.Capabilities[0];
        CaptureAnalysisPolicySnapshot snapshot = await _policyService
            .GetCurrentAsync(cancellationToken)
            .ConfigureAwait(false);
        AnalysisProcessingPolicy? processingPolicy = snapshot.Policy?.ProcessingPolicy;
        if (processingPolicy == null)
        {
            return null;
        }

        ProcessingBoundary boundary = processingPolicy.AllowedBoundaries.Contains(ProcessingBoundary.OnDevice)
            ? ProcessingBoundary.OnDevice
            : processingPolicy.AllowedBoundaries[0];
        AnalyzerIdentity? analyzer = boundary == ProcessingBoundary.Remote
            ? _analyzers.Analyzers.FirstOrDefault(candidate =>
                candidate.Descriptor.Capability == capability.Capability &&
                candidate.Descriptor.ProcessingBoundary == boundary)?.Descriptor.Identity
            : null;
        if (boundary == ProcessingBoundary.Remote && analyzer == null)
        {
            return null;
        }

        VerifiedState? verified = await TryAuthorizeAndVerifyAsync(
            registration.Preconditions,
            capability.Capability,
            boundary,
            analyzer,
            CaptureAnalysisAuthorizationStage.SourceVerification,
            cancellationToken).ConfigureAwait(false);
        if (verified == null ||
            verified.Asset.MediaType != MapMediaType(registration.MediaKind) ||
            verified.Asset.CapturedAtUtc != registration.CapturedAtUtc)
        {
            if (verified != null)
            {
                await verified.Source.DisposeAsync().ConfigureAwait(false);
            }

            return null;
        }

        return new(
            verified.Asset,
            verified.Source,
            capability.Capability,
            boundary,
            analyzer);
    }

    private bool ReconcileProducerRevisions(
        CaptureAnalysisRecord record,
        IEnumerable<RecipeCapability> capabilities,
        ProcessingBoundary boundary)
    {
        bool unchanged = true;
        foreach (RecipeCapability requested in capabilities)
        {
            AnalyzerRevision[] currentRevisions = _analyzers.Analyzers
                .Where(analyzer =>
                    analyzer.Descriptor.Capability == requested.Capability &&
                    analyzer.Descriptor.ProcessingBoundary == boundary &&
                    _featureAvailability.IsAnalyzerEnabled(analyzer.Descriptor.Identity))
                .Select(analyzer => analyzer.Descriptor.Revision)
                .Distinct()
                .ToArray();
            unchanged &= !record.InvalidateCapability(
                requested.Capability,
                currentRevisions);
        }

        return unchanged;
    }

    private async ValueTask<VerifiedState?> TryAuthorizeAndVerifyAsync(
        AnalysisCommitPreconditions expected,
        CapabilityDefinition capability,
        ProcessingBoundary boundary,
        AnalyzerIdentity? analyzer,
        CaptureAnalysisAuthorizationStage stage,
        CancellationToken cancellationToken)
    {
        var operationRequest = new CaptureAnalysisAuthorizationRequest(
            expected.CaptureId,
            expected.Purpose,
            capability,
            boundary,
            analyzer,
            stage);
        CaptureAnalysisAuthorizationDecision operationAuthorization = await _policyService
            .AuthorizeAsync(operationRequest, cancellationToken)
            .ConfigureAwait(false);
        if (!Matches(expected, operationAuthorization))
        {
            return null;
        }

        var verificationRequest = new CaptureAnalysisAuthorizationRequest(
            expected.CaptureId,
            expected.Purpose,
            capability,
            boundary,
            analyzer,
            CaptureAnalysisAuthorizationStage.SourceVerification);
        CaptureAnalysisAuthorizationDecision verificationAuthorization = stage ==
            CaptureAnalysisAuthorizationStage.SourceVerification
                ? operationAuthorization
                : await _policyService.AuthorizeAsync(verificationRequest, cancellationToken)
                    .ConfigureAwait(false);
        if (!Matches(expected, verificationAuthorization))
        {
            return null;
        }

        IVerifiedCaptureAnalysisSource? source = await _sourceVerifier.TryOpenVerifiedAsync(
            new CaptureAnalysisSourceVerificationRequest(verificationAuthorization),
            cancellationToken).ConfigureAwait(false);
        if (source == null)
        {
            return null;
        }

        CaptureAsset? asset = _captureAssets.Get(expected.CaptureId);
        if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active } ||
            source.CaptureSourceGeneration != expected.CaptureSourceGeneration ||
            source.SourceStamp != expected.SourceStamp ||
            source.SourceRevision != expected.SourceRevision ||
            expected.ResolutionPolicyRevision != _featureAvailability.ResolutionPolicyRevision)
        {
            await source.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        return new(asset, source, capability, boundary, analyzer);
    }

    private async ValueTask<bool> IsStillAuthorizedAsync(
        AnalysisCommitPreconditions expected,
        CapabilityDefinition capability,
        ProcessingBoundary boundary,
        AnalyzerIdentity? analyzer,
        CaptureAnalysisAuthorizationStage stage,
        CancellationToken cancellationToken)
    {
        var request = new CaptureAnalysisAuthorizationRequest(
            expected.CaptureId,
            expected.Purpose,
            capability,
            boundary,
            analyzer,
            stage);
        CaptureAnalysisAuthorizationDecision authorization = await _policyService
            .AuthorizeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);
        CaptureAnalysisEnrollment? enrollment = control.State.Enrollments.FirstOrDefault(
            candidate => candidate.CaptureId == expected.CaptureId);
        CaptureAsset? asset = _captureAssets.Get(expected.CaptureId);
        return Matches(expected, authorization) &&
            control.State.PolicyRevision == expected.PolicyRevision &&
            control.State.ControlGeneration == expected.ControlGeneration &&
            enrollment is { State: CaptureAnalysisEnrollmentState.Enrolled } &&
            enrollment.EnrollmentGeneration == expected.EnrollmentGeneration &&
            enrollment.TombstoneGeneration == expected.TombstoneGeneration &&
            enrollment.RequestedRecipeId == expected.RecipeId &&
            enrollment.RequestedRecipeVersion == expected.RecipeVersion &&
            asset is { LifecycleState: CaptureAssetLifecycleState.Active } &&
            GetSourceGeneration(expected.CaptureId) == expected.CaptureSourceGeneration &&
            expected.ResolutionPolicyRevision == _featureAvailability.ResolutionPolicyRevision;
    }

    private static bool Matches(
        AnalysisCommitPreconditions expected,
        CaptureAnalysisAuthorizationDecision authorization)
    {
        return authorization.IsAuthorized &&
            authorization.PolicyRevision == expected.PolicyRevision &&
            authorization.ControlGeneration == expected.ControlGeneration &&
            authorization.EnrollmentGeneration == expected.EnrollmentGeneration &&
            authorization.TombstoneGeneration == expected.TombstoneGeneration;
    }

    private SemaphoreSlim GetGate(CaptureId captureId)
    {
        return _captureGates.GetOrAdd(captureId, static _ => new SemaphoreSlim(1, 1));
    }

    private long GetSourceGeneration(CaptureId captureId)
    {
        return _captureAssets.GetChangesAfter(0)
            .Where(change => change.CaptureId == captureId && change.ChangeType is
                CaptureAssetChangeType.Finalized or
                CaptureAssetChangeType.SourceChanged or
                CaptureAssetChangeType.Deleted)
            .Select(change => change.Sequence)
            .LastOrDefault();
    }

    private static CaptureFileType MapMediaType(CaptureMediaKind mediaKind)
    {
        return mediaKind switch
        {
            CaptureMediaKind.Image => CaptureFileType.Image,
            CaptureMediaKind.Audio => CaptureFileType.Audio,
            CaptureMediaKind.Video => CaptureFileType.Video,
            _ => CaptureFileType.Unknown,
        };
    }

    private sealed record VerifiedState(
        CaptureAsset Asset,
        IVerifiedCaptureAnalysisSource Source,
        CapabilityDefinition Capability,
        ProcessingBoundary Boundary,
        AnalyzerIdentity? Analyzer);
}
