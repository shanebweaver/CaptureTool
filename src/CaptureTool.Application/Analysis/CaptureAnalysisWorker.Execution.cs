using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed partial class CaptureAnalysisWorker
{
    private async Task ProcessAsync(AnalysisWorkItem queued, int remaining, CancellationToken shutdown)
    {
        AnalysisWorkItem? work = await _store.GetWorkAsync(queued.Token.CaptureId, shutdown).ConfigureAwait(false);
        if (work == null || work.Token != queued.Token || !work.Run.IsPending)
        {
            return;
        }

        CancellationToken revoked;
        using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(shutdown).ConfigureAwait(false))
        {
            if (!IsAuthorized(grant, work))
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
                Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, LastRunStatus: AnalysisRunStatus.Cancelled));
                return;
            }
            revoked = grant.Revoked;
        }
        MediaAnalysisPlan? plan = GetPlanFor(work);
        if (plan == null || plan.Version != work.Run.PlanVersion || !plan.Steps.Select(step => step.Capability).SequenceEqual(work.Run.Steps))
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.Failed, shutdown).ConfigureAwait(false);
            Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, FailureCode: "plan-changed", LastRunStatus: AnalysisRunStatus.Failed));
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown, revoked);
        lock (_activeLock)
        {
            _activeCancellation = cancellation;
            _activeToken = work.Token;
        }
        CancellationToken ct = cancellation.Token;
        IAnalysisSourceLease? source = null;
        try
        {
            Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, work.Run.CompletedSteps.Count, plan.Steps.Count));
            var asset = await _catalog.GetAsync(work.Token.CaptureId, ct).ConfigureAwait(false);
            string? relocatedPath = work.UsePreferredPath ? asset?.PreferredPath : asset?.SourcePath;
            source = await OpenSourceAsync(work, relocatedPath, ct).ConfigureAwait(false);
            if (work.Run.SourceRevision != null && source.Revision != work.Run.SourceRevision)
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
                return;
            }
            using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(ct).ConfigureAwait(false))
            {
                if (!IsAuthorized(grant, work) || !await _store.BindSourceAsync(work.Token, source.Revision, ct).ConfigureAwait(false))
                {
                    return;
                }
            }
            DateTimeOffset? capturedAt = asset?.CapturedAt;
            var input = new AnalysisInput(work.Token.CaptureId, work.MediaKind, source.Revision, source.Path, work.Language, capturedAt);
            int index = work.Run.CompletedSteps.Count;
            ct.ThrowIfCancellationRequested();
            AnalysisWorkItem? current = await _store.GetWorkAsync(work.Token.CaptureId, ct).ConfigureAwait(false);
            if (current?.Token != work.Token || !current.Run.IsPending)
            {
                return;
            }

            AnalysisStep step = plan.Steps[index];
            if (await TryReuseResultAsync(work, step, source, ct, shutdown).ConfigureAwait(false))
            {
                return;
            }

            CaptureAnalysisRecord? metadata = _steps.RequiresMetadata(step)
                ? await _store.GetAsync(work.Token.CaptureId, source.Revision, ct).ConfigureAwait(false) : null;
            var stepProgress = new AnalysisActivitySnapshot(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, index, plan.Steps.Count);
            AnalysisStepResult result = await _steps.ExecuteAsync(step, input, metadata,
                () => BeginStepProgress(stepProgress, ct), ct).ConfigureAwait(false);
            await PublishStepAsync(work, step, source, result, ct, shutdown).ConfigureAwait(false);
            // Rejoin the batch at the next durable step boundary.
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
        }
        catch (AnalysisProviderBusyException)
        {
            await _store.FinishAsync(work.Token, ct.IsCancellationRequested ? AnalysisRunStatus.Cancelled : AnalysisRunStatus.Failed, shutdown).ConfigureAwait(false);
            Report(new(AnalysisActivity.Analyzing, remaining, FailureCode: "provider-not-stopped"));
        }
        catch (SourceUnavailableException)
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
        }
        finally
        {
            AdvanceProgressVersion();
            lock (_activeLock)
            {
                _activeCancellation = null;
                _activeToken = null;
            }
            if (source != null)
            {
                await _providers.ReleaseSourceAsync(source).ConfigureAwait(false);
            }
            if (!shutdown.IsCancellationRequested)
            {
                await ReportFinishedRunAsync(work.Token, remaining, plan.Steps.Count, shutdown).ConfigureAwait(false);
            }
        }
    }

    private async Task PublishStepAsync(AnalysisWorkItem work, AnalysisStep step, IAnalysisSourceLease source,
        AnalysisStepResult execution, CancellationToken cancellationToken, CancellationToken shutdown)
    {
        AnalyzerOutcome outcome = execution.Outcome;
        if (outcome.Kind is AnalyzerOutcomeKind.Cancelled or AnalyzerOutcomeKind.InvalidSource)
        {
            AnalysisRunStatus status = outcome.Kind == AnalyzerOutcomeKind.Cancelled
                ? AnalysisRunStatus.Cancelled
                : AnalysisRunStatus.InvalidSource;
            await _store.FinishAsync(work.Token, status, shutdown).ConfigureAwait(false);
            return;
        }

        if (!await VerifySourceAsync(source, cancellationToken).ConfigureAwait(false))
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
            return;
        }

        using IAnalysisAuthorizationLease publication = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (!IsAuthorized(publication, work))
        {
            return;
        }

        AnalysisResult? result = outcome.Kind == AnalyzerOutcomeKind.Succeeded
            ? new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, work.Run.PlanVersion, work.Run.Id, derivation: execution.Derivation)
            : null;
        var completion = new AnalysisStepCompletion(step.Capability, outcome.Kind, outcome.FailureCode);
        bool committed = await _store.CommitStepAsync(work.Token, completion, result, cancellationToken).ConfigureAwait(false);
        if (committed && result != null)
        {
            NotifyResultCommitted(work.Token.CaptureId, result.Payload.Capability);
        }
    }

    // Returning true means the cached-result path handled this step, including a
    // rejected publication. It must never fall through to an inference retry.
    private async Task<bool> TryReuseResultAsync(AnalysisWorkItem work, AnalysisStep step,
        IAnalysisSourceLease source, CancellationToken cancellationToken, CancellationToken shutdown)
    {
        if (!work.ReuseExisting)
        {
            return false;
        }

        CaptureAnalysisRecord? saved = await _store.GetAsync(work.Token.CaptureId, source.Revision, cancellationToken).ConfigureAwait(false);
        AnalysisResult? reusable = saved?.Results.SingleOrDefault(result => result.Payload.Capability == step.Capability);
        if (reusable == null)
        {
            return false;
        }

        if (!await VerifySourceAsync(source, cancellationToken).ConfigureAwait(false))
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
            return true;
        }

        using IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (IsAuthorized(grant, work))
        {
            var completion = new AnalysisStepCompletion(step.Capability, AnalyzerOutcomeKind.Succeeded, null, reusable.ResultId);
            await _store.CommitStepAsync(work.Token, completion, null, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private MediaAnalysisPlan? GetPlanFor(AnalysisWorkItem work)
    {
        try
        {
            return _configuration.SelectPlan(work.MediaKind, work.Run.Steps);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
