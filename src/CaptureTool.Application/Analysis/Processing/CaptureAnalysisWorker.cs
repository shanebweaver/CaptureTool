using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Processing;

internal sealed class CaptureAnalysisWorker : ICaptureAnalysisWorker
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromMinutes(15);
    private const int MaximumAttempts = 8;

    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisWakeWaiter _wakeWaiter;
    private readonly ICaptureAnalyzerResolver _resolver;
    private readonly ICaptureAnalyzerCatalog _analyzers;
    private readonly ICaptureAnalysisPolicyService _policyService;
    private readonly ICaptureAnalysisSourceVerifier _sourceVerifier;
    private readonly ICaptureAnalysisMutationCoordinator _mutationCoordinator;
    private readonly ICaptureAnalysisStore _metadataStore;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ICaptureAnalysisProjectionRefresher _projectionRefresher;
    private readonly IClock _clock;
    private readonly ILogService _logService;

    public CaptureAnalysisWorker(
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisWakeWaiter wakeWaiter,
        ICaptureAnalyzerResolver resolver,
        ICaptureAnalyzerCatalog analyzers,
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisSourceVerifier sourceVerifier,
        ICaptureAnalysisMutationCoordinator mutationCoordinator,
        ICaptureAnalysisStore metadataStore,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ICaptureAnalysisProjectionRefresher projectionRefresher,
        IClock clock,
        ILogService logService)
    {
        _jobStore = jobStore;
        _wakeWaiter = wakeWaiter;
        _resolver = resolver;
        _analyzers = analyzers;
        _policyService = policyService;
        _sourceVerifier = sourceVerifier;
        _mutationCoordinator = mutationCoordinator;
        _metadataStore = metadataStore;
        _featureAvailability = featureAvailability;
        _projectionRefresher = projectionRefresher;
        _clock = clock;
        _logService = logService;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshCompletedProjectionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logService.LogException(
                exception,
                "Failed to recover completed Capture Analysis projections.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DateTimeOffset now = GetUtcNow();
                _ = await _jobStore.RecoverExpiredLeasesAsync(now, cancellationToken)
                    .ConfigureAwait(false);
                CaptureAnalysisJobLease? lease = await _jobStore.TryLeaseNextDueAsync(
                    now,
                    LeaseDuration,
                    cancellationToken).ConfigureAwait(false);
                if (lease != null)
                {
                    await ProcessAsync(lease, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                DateTimeOffset? nextDue = await _jobStore.GetNextDueTimeAsync(cancellationToken)
                    .ConfigureAwait(false);
                TimeSpan delay = nextDue.HasValue
                    ? Max(TimeSpan.Zero, nextDue.Value - GetUtcNow())
                    : Timeout.InfiniteTimeSpan;
                await _wakeWaiter.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logService.LogException(exception, "Capture Analysis worker iteration failed.");
                await Task.Delay(FailureDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessAsync(
        CaptureAnalysisJobLease lease,
        CancellationToken cancellationToken)
    {
        CaptureAnalysisJobIntent intent = lease.Intent;
        AnalysisCommitPreconditions expected = intent.Key.Preconditions;
        if (expected.ResolutionPolicyRevision != _featureAvailability.ResolutionPolicyRevision)
        {
            _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
            return;
        }

        CaptureAnalysisStoreSnapshot? registeredSource = await _metadataStore
            .GetAsync(intent.Key.CaptureId, cancellationToken)
            .ConfigureAwait(false);
        if (registeredSource == null ||
            registeredSource.Record.SourceRevision != expected.SourceRevision ||
            registeredSource.Record.Recipe.Id != expected.RecipeId ||
            registeredSource.Record.Recipe.Version != expected.RecipeVersion)
        {
            _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
            return;
        }

        CaptureMediaKind mediaKind = registeredSource.Record.MediaKind;

        AnalysisProcessingPolicy? processingPolicy = await TryGetAuthorizedPolicyAsync(
            intent.Key,
            CaptureAnalysisAuthorizationStage.AnalyzerAvailability,
            cancellationToken).ConfigureAwait(false);
        if (processingPolicy == null)
        {
            _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
            return;
        }

        var attempted = intent.Attempts
            .Where(attempt => attempt.Status is
                CaptureAnalyzerAttemptStatus.Unsupported or CaptureAnalyzerAttemptStatus.TerminalFailure)
            .Select(attempt => attempt.AnalyzerRevision)
            .ToHashSet();
        AnalysisFailure? latestTransientFailure = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            CaptureAnalyzerResolution resolution = await _resolver.ResolveAsync(
                new CaptureAnalyzerResolutionRequest(
                    intent.Key.Capability,
                    mediaKind,
                    expected.SourceRevision.Length,
                    expected.Purpose,
                    RestrictToBoundary(processingPolicy, intent.Key.AuthorizedProcessingBoundary),
                    expected.ResolutionPolicyRevision,
                    attempted),
                cancellationToken).ConfigureAwait(false);
            if (resolution.Status == CaptureAnalyzerResolutionStatus.WaitingForPreparation)
            {
                _ = await _jobStore.TryWaitForCapabilityAsync(
                    lease.LeaseToken,
                    new AnalysisFailure(
                        AnalysisFailureCode.ModelNotReady,
                        AnalysisFailureDisposition.Transient),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (resolution.Status != CaptureAnalyzerResolutionStatus.Resolved ||
                resolution.Analyzer == null)
            {
                if (latestTransientFailure.HasValue)
                {
                    await ScheduleRetryAsync(
                        lease.LeaseToken,
                        intent,
                        latestTransientFailure.Value,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (intent.Attempts.LastOrDefault() is { Failure: { } terminalFailure } latest &&
                    latest.Status is CaptureAnalyzerAttemptStatus.Unsupported or
                        CaptureAnalyzerAttemptStatus.TerminalFailure)
                {
                    await CommitOutcomeAndFailAsync(
                        lease.LeaseToken,
                        intent,
                        latest.Analyzer,
                        terminalFailure,
                        latest.Status == CaptureAnalyzerAttemptStatus.Unsupported,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _ = await _jobStore.TryWaitForCapabilityAsync(
                        lease.LeaseToken,
                        new AnalysisFailure(
                            AnalysisFailureCode.CapabilityUnavailable,
                            AnalysisFailureDisposition.Transient),
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            ICaptureAnalyzer analyzer = resolution.Analyzer;
            CaptureAnalysisAuthorizationDecision? invocation = await TryAuthorizeAsync(
                intent.Key,
                analyzer.Descriptor.Identity,
                CaptureAnalysisAuthorizationStage.AnalyzerInvocation,
                cancellationToken).ConfigureAwait(false);
            if (invocation == null)
            {
                attempted.Add(analyzer.Descriptor.Revision);
                continue;
            }

            if (await IsAlreadyCommittedAsync(intent.Key, analyzer, cancellationToken)
                .ConfigureAwait(false))
            {
                if (intent.Attempts.LastOrDefault()?.Status != CaptureAnalyzerAttemptStatus.Succeeded)
                {
                    DateTimeOffset recoveredAtUtc = GetUtcNow();
                    var recoveredAttempt = new CaptureAnalyzerAttempt(
                        intent.AttemptCount + 1,
                        analyzer.Descriptor.Identity,
                        intent.Key.AuthorizedProcessingBoundary,
                        recoveredAtUtc,
                        recoveredAtUtc,
                        CaptureAnalyzerAttemptStatus.Succeeded,
                        failure: null);
                    CaptureAnalysisJobMutationResult recovered = await _jobStore.TryRecordAttemptAsync(
                        lease.LeaseToken,
                        recoveredAttempt,
                        cancellationToken).ConfigureAwait(false);
                    if (recovered.Status != CaptureAnalysisJobMutationStatus.Succeeded)
                    {
                        return;
                    }

                    intent = recovered.Intent!;
                }

                CaptureAnalysisJobMutationResult completed = await _jobStore.TryCompleteAsync(
                    lease.LeaseToken,
                    cancellationToken).ConfigureAwait(false);
                if (completed.Status == CaptureAnalysisJobMutationStatus.Succeeded)
                {
                    await TryRefreshProjectionAsync(intent.Key.CaptureId, cancellationToken)
                        .ConfigureAwait(false);
                }

                return;
            }

            CaptureAnalysisAuthorizationDecision? sourceAuthorization = await TryAuthorizeAsync(
                intent.Key,
                analyzer.Descriptor.Identity,
                CaptureAnalysisAuthorizationStage.SourceVerification,
                cancellationToken).ConfigureAwait(false);
            if (sourceAuthorization == null)
            {
                _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
                return;
            }

            IVerifiedCaptureAnalysisSource? source = await _sourceVerifier.TryOpenVerifiedAsync(
                new CaptureAnalysisSourceVerificationRequest(sourceAuthorization),
                cancellationToken).ConfigureAwait(false);
            if (source == null || !Matches(expected, source))
            {
                if (source != null)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }

                _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
                return;
            }

            DateTimeOffset startedAtUtc = GetUtcNow();
            CaptureAnalyzerOutput output;
            await using (source.ConfigureAwait(false))
            {
                try
                {
                    output = await analyzer.AnalyzeAsync(
                        new CaptureAnalysisRequest(
                            analyzer.Descriptor,
                            expected.Purpose,
                            invocation.ProcessingPolicy!,
                            source),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logService.LogException(exception, "A Capture Analysis provider failed.");
                    output = CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient));
                }
            }

            DateTimeOffset completedAtUtc = GetUtcNow();
            CaptureAnalyzerAttempt attempt = CreateAttempt(
                intent.AttemptCount + 1,
                analyzer.Descriptor.Identity,
                intent.Key.AuthorizedProcessingBoundary,
                startedAtUtc,
                completedAtUtc,
                output);
            CaptureAnalysisJobMutationResult recorded = await _jobStore.TryRecordAttemptAsync(
                lease.LeaseToken,
                attempt,
                cancellationToken).ConfigureAwait(false);
            if (recorded.Status != CaptureAnalysisJobMutationStatus.Succeeded)
            {
                return;
            }

            intent = recorded.Intent!;
            if (output.Status == CaptureAnalyzerOutputStatus.Succeeded)
            {
                if (!output.IsCompatibleWith(analyzer.Descriptor))
                {
                    AnalysisFailure invalidResponse = new(
                        AnalysisFailureCode.InvalidResponse,
                        AnalysisFailureDisposition.Terminal);
                    var terminalAttempt = new CaptureAnalyzerAttempt(
                        intent.AttemptCount + 1,
                        analyzer.Descriptor.Identity,
                        intent.Key.AuthorizedProcessingBoundary,
                        completedAtUtc,
                        completedAtUtc,
                        CaptureAnalyzerAttemptStatus.TerminalFailure,
                        invalidResponse);
                    CaptureAnalysisJobMutationResult terminalRecorded = await _jobStore
                        .TryRecordAttemptAsync(
                            lease.LeaseToken,
                            terminalAttempt,
                            cancellationToken).ConfigureAwait(false);
                    if (terminalRecorded.Status != CaptureAnalysisJobMutationStatus.Succeeded)
                    {
                        return;
                    }

                    await CommitOutcomeAndFailAsync(
                        lease.LeaseToken,
                        terminalRecorded.Intent!,
                        analyzer.Descriptor.Identity,
                        invalidResponse,
                        unsupported: false,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                var result = new CanonicalCapabilityResult(
                    expected.CaptureId,
                    expected.SourceRevision,
                    output.Payload!,
                    analyzer.Descriptor.Identity,
                    intent.Key.AuthorizedProcessingBoundary,
                    completedAtUtc);
                CaptureAnalysisStoreWriteStatus commit = await CommitResultAsync(
                    new AnalysisCommitToken(
                        expected,
                        intent.Key.Capability,
                        analyzer.Descriptor.Revision),
                    result,
                    cancellationToken).ConfigureAwait(false);
                if (commit == CaptureAnalysisStoreWriteStatus.Succeeded)
                {
                    CaptureAnalysisJobMutationResult completed = await _jobStore.TryCompleteAsync(
                        lease.LeaseToken,
                        cancellationToken).ConfigureAwait(false);
                    if (completed.Status == CaptureAnalysisJobMutationStatus.Succeeded)
                    {
                        await TryRefreshProjectionAsync(expected.CaptureId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                else if (commit == CaptureAnalysisStoreWriteStatus.StaleCommit)
                {
                    _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await RecordCommitRetryAsync(
                        lease.LeaseToken,
                        intent,
                        analyzer.Descriptor.Identity,
                        completedAtUtc,
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            if (output.Status == CaptureAnalyzerOutputStatus.Cancelled)
            {
                _ = await _jobStore.TryCancelAsync(intent.Key, cancellationToken).ConfigureAwait(false);
                return;
            }

            AnalysisFailure failure = output.Failure!.Value;
            attempted.Add(analyzer.Descriptor.Revision);
            if (failure.Disposition == AnalysisFailureDisposition.Transient)
            {
                latestTransientFailure = failure;
                if (intent.AttemptCount >= MaximumAttempts)
                {
                    var exhausted = new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Terminal);
                    CaptureAnalyzerAttempt terminalAttempt = new(
                        intent.AttemptCount + 1,
                        analyzer.Descriptor.Identity,
                        intent.Key.AuthorizedProcessingBoundary,
                        completedAtUtc,
                        completedAtUtc,
                        CaptureAnalyzerAttemptStatus.TerminalFailure,
                        exhausted);
                    CaptureAnalysisJobMutationResult terminalRecorded = await _jobStore
                        .TryRecordAttemptAsync(lease.LeaseToken, terminalAttempt, cancellationToken)
                        .ConfigureAwait(false);
                    if (terminalRecorded.Status == CaptureAnalysisJobMutationStatus.Succeeded)
                    {
                        await CommitOutcomeAndFailAsync(
                            lease.LeaseToken,
                            terminalRecorded.Intent!,
                            analyzer.Descriptor.Identity,
                            exhausted,
                            unsupported: false,
                            cancellationToken).ConfigureAwait(false);
                    }

                    return;
                }

                continue;
            }
        }
    }

    private async ValueTask<AnalysisProcessingPolicy?> TryGetAuthorizedPolicyAsync(
        CaptureAnalysisJobKey key,
        CaptureAnalysisAuthorizationStage stage,
        CancellationToken cancellationToken)
    {
        foreach (ICaptureAnalyzer analyzer in _analyzers.Analyzers.Where(candidate =>
            candidate.Descriptor.Capability == key.Capability &&
            candidate.Descriptor.ProcessingBoundary == key.AuthorizedProcessingBoundary))
        {
            CaptureAnalysisAuthorizationDecision? decision = await TryAuthorizeAsync(
                key,
                analyzer.Descriptor.Identity,
                stage,
                cancellationToken).ConfigureAwait(false);
            if (decision?.ProcessingPolicy != null)
            {
                return decision.ProcessingPolicy;
            }
        }

        return null;
    }

    private async ValueTask<CaptureAnalysisAuthorizationDecision?> TryAuthorizeAsync(
        CaptureAnalysisJobKey key,
        AnalyzerIdentity analyzer,
        CaptureAnalysisAuthorizationStage stage,
        CancellationToken cancellationToken)
    {
        var request = new CaptureAnalysisAuthorizationRequest(
            key.CaptureId,
            key.Preconditions.Purpose,
            key.Capability,
            key.AuthorizedProcessingBoundary,
            analyzer,
            stage);
        CaptureAnalysisAuthorizationDecision decision = await _policyService
            .AuthorizeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        AnalysisCommitPreconditions expected = key.Preconditions;
        return decision.IsAuthorized &&
            decision.PolicyRevision == expected.PolicyRevision &&
            decision.ControlGeneration == expected.ControlGeneration &&
            decision.EnrollmentGeneration == expected.EnrollmentGeneration &&
            decision.TombstoneGeneration == expected.TombstoneGeneration
                ? decision
                : null;
    }

    private async ValueTask<bool> IsAlreadyCommittedAsync(
        CaptureAnalysisJobKey key,
        ICaptureAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        if (key.Preconditions.CaptureId.IsEmpty)
        {
            return false;
        }

        CaptureAnalysisStoreSnapshot? snapshot = await _metadataStore
            .GetAsync(key.CaptureId, cancellationToken)
            .ConfigureAwait(false);
        return snapshot?.Record.SourceRevision == key.SourceRevision &&
            snapshot.Record.Recipe.Id == key.Preconditions.RecipeId &&
            snapshot.Record.Recipe.Version == key.Preconditions.RecipeVersion &&
            snapshot.Record.TryGetAnalysis(key.Capability.Id, out CapabilityAnalysis? analysis) &&
            analysis?.CanonicalResult is { } result &&
            result.Capability == key.Capability &&
            result.Analyzer.Revision == analyzer.Descriptor.Revision &&
            result.ProcessingBoundary == key.AuthorizedProcessingBoundary;
    }

    private async ValueTask<CaptureAnalysisStoreWriteStatus> CommitResultAsync(
        AnalysisCommitToken token,
        CanonicalCapabilityResult result,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            CaptureAnalysisStoreSnapshot? snapshot = await _metadataStore
                .GetAsync(token.CaptureId, cancellationToken)
                .ConfigureAwait(false);
            if (snapshot == null)
            {
                return CaptureAnalysisStoreWriteStatus.NotFound;
            }

            CaptureAnalysisStoreWriteResult write = await _mutationCoordinator
                .TryCommitCapabilityAsync(
                    token,
                    result,
                    snapshot.DocumentRevision,
                    cancellationToken).ConfigureAwait(false);
            if (write.Status != CaptureAnalysisStoreWriteStatus.Conflict)
            {
                return write.Status;
            }
        }

        return CaptureAnalysisStoreWriteStatus.Conflict;
    }

    private async Task CommitOutcomeAndFailAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalysisJobIntent intent,
        AnalyzerIdentity analyzer,
        AnalysisFailure failure,
        bool unsupported,
        CancellationToken cancellationToken)
    {
        var token = new AnalysisCommitToken(
            intent.Key.Preconditions,
            intent.Key.Capability,
            analyzer.Revision);
        var outcome = new CapabilityOutcome(
            intent.Key.CaptureId,
            intent.Key.SourceRevision,
            intent.Key.Capability,
            analyzer,
            intent.Key.AuthorizedProcessingBoundary,
            unsupported ? CapabilityOutcomeState.Unsupported : CapabilityOutcomeState.TerminalFailure,
            failure,
            GetUtcNow());
        CaptureAnalysisStoreSnapshot? snapshot = await _metadataStore
            .GetAsync(intent.Key.CaptureId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot != null)
        {
            _ = await _mutationCoordinator.TryCommitCapabilityAsync(
                token,
                outcome,
                snapshot.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
        }

        _ = await _jobStore.TryFailTerminalAsync(leaseToken, failure, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RecordCommitRetryAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalysisJobIntent intent,
        AnalyzerIdentity analyzer,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var failure = new AnalysisFailure(
            AnalysisFailureCode.InternalError,
            AnalysisFailureDisposition.Transient);
        var attempt = new CaptureAnalyzerAttempt(
            intent.AttemptCount + 1,
            analyzer,
            intent.Key.AuthorizedProcessingBoundary,
            timestamp,
            timestamp,
            CaptureAnalyzerAttemptStatus.TransientFailure,
            failure);
        CaptureAnalysisJobMutationResult recorded = await _jobStore.TryRecordAttemptAsync(
            leaseToken,
            attempt,
            cancellationToken).ConfigureAwait(false);
        if (recorded.Status == CaptureAnalysisJobMutationStatus.Succeeded)
        {
            await ScheduleRetryAsync(
                leaseToken,
                recorded.Intent!,
                failure,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ScheduleRetryAsync(
        CaptureAnalysisJobLeaseToken leaseToken,
        CaptureAnalysisJobIntent intent,
        AnalysisFailure failure,
        CancellationToken cancellationToken)
    {
        double multiplier = Math.Pow(2, Math.Min(intent.AttemptCount, 6));
        TimeSpan retryDelay = TimeSpan.FromTicks(Math.Min(
            (long)(InitialRetryDelay.Ticks * multiplier),
            MaximumRetryDelay.Ticks));
        _ = await _jobStore.TryScheduleRetryAsync(
            leaseToken,
            failure,
            GetUtcNow() + retryDelay,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshCompletedProjectionsAsync(CancellationToken cancellationToken)
    {
        await foreach (CaptureAnalysisJobIntent intent in _jobStore
            .ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (intent.State == CaptureAnalysisJobState.Completed)
            {
                await TryRefreshProjectionAsync(intent.Key.CaptureId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task TryRefreshProjectionAsync(
        CaptureTool.Domain.CaptureId captureId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _projectionRefresher.RefreshAsync(captureId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, "Failed to refresh a Capture Analysis projection.");
        }
    }

    private static CaptureAnalyzerAttempt CreateAttempt(
        int number,
        AnalyzerIdentity analyzer,
        ProcessingBoundary boundary,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        CaptureAnalyzerOutput output)
    {
        CaptureAnalyzerAttemptStatus status = output.Status switch
        {
            CaptureAnalyzerOutputStatus.Succeeded => CaptureAnalyzerAttemptStatus.Succeeded,
            CaptureAnalyzerOutputStatus.Unsupported => CaptureAnalyzerAttemptStatus.Unsupported,
            CaptureAnalyzerOutputStatus.Cancelled => CaptureAnalyzerAttemptStatus.Cancelled,
            CaptureAnalyzerOutputStatus.Failed when
                output.Failure?.Disposition == AnalysisFailureDisposition.Transient =>
                    CaptureAnalyzerAttemptStatus.TransientFailure,
            CaptureAnalyzerOutputStatus.Failed => CaptureAnalyzerAttemptStatus.TerminalFailure,
            _ => throw new InvalidOperationException("The provider returned an unknown output status."),
        };
        return new(
            number,
            analyzer,
            boundary,
            startedAtUtc,
            completedAtUtc,
            status,
            output.Failure);
    }

    private static AnalysisProcessingPolicy RestrictToBoundary(
        AnalysisProcessingPolicy policy,
        ProcessingBoundary boundary)
    {
        return boundary == ProcessingBoundary.Remote
            ? new AnalysisProcessingPolicy(
                policy.AuthorizedPurpose,
                [boundary],
                policy.AllowedRemoteProviderIds)
            : new AnalysisProcessingPolicy(policy.AuthorizedPurpose, [boundary]);
    }

    private static bool Matches(
        AnalysisCommitPreconditions expected,
        IVerifiedCaptureAnalysisSource source)
    {
        return source.CaptureId == expected.CaptureId &&
            source.CaptureSourceGeneration == expected.CaptureSourceGeneration &&
            source.SourceStamp == expected.SourceStamp &&
            source.SourceRevision == expected.SourceRevision;
    }

    private DateTimeOffset GetUtcNow()
    {
        return new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return left >= right ? left : right;
    }
}

internal sealed class CaptureAnalysisWorkerHost : IDisposable
{
    private readonly ICaptureAnalysisWorker _worker;
    private readonly ICancellationService _cancellationService;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly ILogService _logService;
    private CancellationTokenSource? _cancellation;
    private Task? _workerTask;

    public CaptureAnalysisWorkerHost(
        ICaptureAnalysisWorker worker,
        ICancellationService cancellationService,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        ILogService logService)
    {
        _worker = worker;
        _cancellationService = cancellationService;
        _featureAvailability = featureAvailability;
        _logService = logService;
    }

    public void Start()
    {
        if (_workerTask != null || !_featureAvailability.IsCaptureAnalysisEnabled)
        {
            return;
        }

        _cancellation = _cancellationService.GetLinkedCancellationTokenSource();
        _workerTask = RunSafelyAsync(_cancellation.Token);
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _worker.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, "Capture Analysis worker stopped unexpectedly.");
        }
    }
}
