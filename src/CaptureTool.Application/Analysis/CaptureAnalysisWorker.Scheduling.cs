using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed partial class CaptureAnalysisWorker
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Only one analysis worker may run.");
        }

        try
        {
            await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
            long? idleSince = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<AnalysisWorkItem> pending = await _store.ReadPendingAsync(cancellationToken).ConfigureAwait(false);
                if (pending.Count == 0)
                {
                    idleSince ??= _time.GetTimestamp();
                    await WaitWhileIdleAsync(idleSince.Value, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                idleSince = null;
                if (_providers.PendingInvocation is { } invocation)
                {
                    await WaitForProviderAsync(invocation, pending, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                await ProcessBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            Report(new(AnalysisActivity.StorageUnavailable, FailureCode: "storage-unavailable"));
        }
        finally
        {
            // Shutdown abandons active and queued requests. Committed metadata remains,
            // but a late provider result can no longer publish against these tokens.
            try
            {
                await _store.DiscardPendingAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await _providers.ShutdownAsync().ConfigureAwait(false);

                if (Progress.Activity != AnalysisActivity.StorageUnavailable)
                {
                    Report(Progress with
                    {
                        Activity = AnalysisActivity.Idle,
                        QueuedCaptures = 0,
                        Fraction = null
                    });
                }

                _running.Release();
            }
        }
    }

    private async Task WaitWhileIdleAsync(long idleSince, CancellationToken cancellationToken)
    {
        TimeSpan remaining = _providers.IdleRetention - _time.GetElapsedTime(idleSince);
        bool forceRelease = Interlocked.Exchange(ref _releaseRequested, 0) != 0;
        bool canRetainModels = !forceRelease && remaining > TimeSpan.Zero &&
            _providers.PendingInvocation == null && !_providers.IsUnderMemoryPressure;

        if (canRetainModels)
        {
            bool allowed;
            using (var grant = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                allowed = grant.IsAllowed && !grant.Revoked.IsCancellationRequested;
            }

            if (allowed)
            {
                if (Progress.Activity != AnalysisActivity.Idle || Progress.QueuedCaptures != 0 || Progress.Fraction != null)
                {
                    Report(Progress with
                    {
                        Activity = AnalysisActivity.Idle,
                        QueuedCaptures = 0,
                        Fraction = null
                    });
                }

                // New requests wake immediately. Poll during the retention period so
                // low memory or revoked consent releases an idle model promptly.
                TimeSpan delay = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
                await WaitForIdleSignalAsync(delay, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        bool released = await _providers.ReleaseResourcesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        Report(Progress with
        {
            Activity = released ? AnalysisActivity.Idle : AnalysisActivity.ProviderUnavailable,
            FailureCode = released ? Progress.FailureCode : "model-release-failed",
            QueuedCaptures = 0,
            Fraction = null,
        });
        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForIdleSignalAsync(TimeSpan delay, CancellationToken ct)
    {
        // An idle poll is a normal wakeup, not cancellation. Dispose the timer after
        // either wakeup so it cannot leave a callback behind when the worker stops.
        await using var timer = _time.CreateTimer(static state => ((CaptureAnalysisWorker)state!).Signal(),
            this, delay, Timeout.InfiniteTimeSpan);
        await _signal.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task ProcessBatchAsync(IReadOnlyList<AnalysisWorkItem> pending, CancellationToken ct)
    {
        // Bound latency for new arrivals and avoid holding every library source open.
        // Each capture retains its configured order; compatible next steps run together.
        var batch = pending.Take(_configuration.MaximumBatchCaptures).ToList();
        int waiting = pending.Count - batch.Count;
        while (batch.Count != 0)
        {
            AnalysisStep? first = NextStep(batch[0]);
            AnalysisWorkItem[] ready = first == null ? [batch[0]] : batch.Where(work =>
                NextStep(work) is { } step && step.Capability == first.Capability && step.Candidates.SequenceEqual(first.Candidates)).ToArray();
            foreach (AnalysisWorkItem work in ready)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessAsync(work, waiting + batch.Count - 1, ct).ConfigureAwait(false);
                AnalysisWorkItem? next = await _store.GetWorkAsync(work.Token.CaptureId, ct).ConfigureAwait(false);
                int index = batch.IndexOf(work);
                if (next?.Token == work.Token && next.Run.IsPending)
                {
                    if (next.Run.CompletedSteps.Count <= work.Run.CompletedSteps.Count)
                    {
                        // A rejected commit must not become an unbounded inference retry.
                        await _store.FinishAsync(work.Token, AnalysisRunStatus.Failed, ct).ConfigureAwait(false);
                        _log?.LogWarning("Capture analysis stopped a run that made no durable progress.");
                        Report(new(AnalysisActivity.Analyzing, waiting + batch.Count - 1, work.Token.CaptureId,
                            FailureCode: "commit-rejected", LastRunStatus: AnalysisRunStatus.Failed));
                        batch.RemoveAt(index);
                    }
                    else
                    {
                        batch[index] = next;
                    }
                }
                else
                {
                    batch.RemoveAt(index);
                }

                if (_providers.PendingInvocation != null)
                {
                    return;
                }
            }
        }

        AnalysisStep? NextStep(AnalysisWorkItem work)
        {
            MediaAnalysisPlan? plan = GetPlanFor(work);
            return plan?.Version == work.Run.PlanVersion && work.Run.CompletedSteps.Count < plan.Steps.Count
                ? plan.Steps[work.Run.CompletedSteps.Count] : null;
        }
    }
}
