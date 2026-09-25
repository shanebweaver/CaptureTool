using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed class CaptureAnalysisWorker : ICaptureAnalysisWorker, IDisposable
{
    private readonly IAnalysisExecutionStore _store;
    private readonly IAnalysisAuthorization _authorization;
    private readonly IAnalysisSource _source;
    private readonly ICaptureAssetCatalog _catalog;
    private readonly CaptureAnalysisConfiguration _configuration;
    private readonly IReadOnlyDictionary<string, IMediaAnalyzer> _analyzers;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly object _activeLock = new();
    private readonly object _progressLock = new();
    private CancellationTokenSource? _activeCancellation;
    private AnalysisRunToken? _activeToken;
    private Task? _invocation;
    private long _progressVersion;
    private bool _publishingProgress;
    private AnalysisActivitySnapshot _progress = new(AnalysisActivity.Idle);

    public CaptureAnalysisWorker(IAnalysisExecutionStore store, IAnalysisAuthorization authorization, IAnalysisSource source,
        CaptureAnalysisConfiguration configuration, IEnumerable<IMediaAnalyzer> analyzers, ICaptureAssetCatalog catalog)
    {
        _store = store;
        _authorization = authorization;
        _source = source;
        _catalog = catalog;
        _configuration = configuration;
        IMediaAnalyzer[] adapters = analyzers.ToArray();
        configuration.ValidateAnalyzers(adapters.Select(analyzer => analyzer.Descriptor));
        _analyzers = adapters.ToDictionary(analyzer => analyzer.Descriptor.Id, StringComparer.Ordinal);
    }

    public AnalysisActivitySnapshot Progress => Volatile.Read(ref _progress);
    public event Action<AnalysisActivitySnapshot>? ProgressChanged;

    public async Task<bool> EnqueueAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false);
            if (!grant.IsAllowed || grant.Revision == Guid.Empty || grant.Revoked.IsCancellationRequested ||
                request.ExpectedAuthorizationId != grant.Revision) return false;
            MediaAnalysisPlan plan = _configuration.Plans.Single(plan => plan.MediaKind == request.MediaKind);
            bool accepted = await _store.AdmitAsync(request, grant.Revision, plan, cancellationToken).ConfigureAwait(false);
            if (accepted)
            {
                if (request.ExpectedRunId is { } prior) CancelActive(new(request.CaptureId, request.Generation, prior));
                Signal();
            }
            return accepted;
        }
        finally { _commands.Release(); }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Only one analysis worker may run.");
        try
        {
            await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<AnalysisWorkItem> pending = await _store.ReadPendingAsync(cancellationToken).ConfigureAwait(false);
                if (pending.Count == 0)
                {
                    Report(Progress with { Activity = AnalysisActivity.Idle, QueuedCaptures = 0, Fraction = null });
                    await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (_invocation is { IsCompleted: false } invocation)
                {
                    await WaitForProviderAsync(invocation, pending, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                for (int index = 0; index < pending.Count; index++)
                {
                    await ProcessAsync(pending[index], pending.Count - index - 1, cancellationToken).ConfigureAwait(false);
                    if (_invocation is { IsCompleted: false }) break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            Report(new(AnalysisActivity.StorageUnavailable, FailureCode: "storage-unavailable"));
        }
        finally
        {
            if (Progress.Activity != AnalysisActivity.StorageUnavailable) Report(Progress with { Activity = AnalysisActivity.Idle, QueuedCaptures = 0, Fraction = null });
            _running.Release();
        }
    }

    private async Task WaitForProviderAsync(Task invocation, IReadOnlyList<AnalysisWorkItem> pending, CancellationToken shutdown)
    {
        CancellationToken revoked;
        AnalysisRunToken[] unauthorized;
        using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(shutdown).ConfigureAwait(false))
        {
            unauthorized = pending.Where(work => !Permits(grant, work)).Select(work => work.Token).ToArray();
            revoked = grant.Revoked;
        }
        // These run revisions cannot become authorized again. Release the policy lease
        // before writing a potentially large backlog; store tokens reject replacements.
        foreach (AnalysisRunToken token in unauthorized)
            await _store.FinishAsync(token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
        pending = await _store.ReadPendingAsync(shutdown).ConfigureAwait(false);
        if (pending.Count == 0) return;

        Report(new(AnalysisActivity.ProviderUnavailable, pending.Count, FailureCode: "provider-not-stopped"));
        // Wake on provider completion, queue commands, revocation, or shutdown. Cancel the
        // losing signal wait so it cannot consume a later enqueue/clear notification.
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(shutdown, revoked);
        Task signal = _signal.WaitAsync(wake.Token);
        try { await Task.WhenAny(invocation, signal).ConfigureAwait(false); }
        finally
        {
            await wake.CancelAsync().ConfigureAwait(false);
            try { await signal.ConfigureAwait(false); }
            catch (OperationCanceledException) when (wake.IsCancellationRequested) { }
        }
        shutdown.ThrowIfCancellationRequested();
    }

    public async Task CancelAsync(CaptureId captureId, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisWorkItem? work = await _store.GetWorkAsync(captureId, cancellationToken).ConfigureAwait(false);
            if (work != null)
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, cancellationToken).ConfigureAwait(false);
                CancelActive(work.Token);
            }
            Signal();
        }
        finally { _commands.Release(); }
    }

    public async Task<AnalysisCleanupResult> ClearAsync(long reconciliationBoundary, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AnalysisCleanupResult result = await _store.ClearAsync(reconciliationBoundary, cancellationToken).ConfigureAwait(false);
            CancelActive(null);
            Signal();
            return result;
        }
        finally { _commands.Release(); }
    }

    private async Task ProcessAsync(AnalysisWorkItem queued, int remaining, CancellationToken shutdown)
    {
        AnalysisWorkItem? work = await _store.GetWorkAsync(queued.Token.CaptureId, shutdown).ConfigureAwait(false);
        if (work == null || work.Token != queued.Token || !work.Run.IsPending) return;
        CancellationToken revoked;
        using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(shutdown).ConfigureAwait(false))
        {
            if (!Permits(grant, work))
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
                Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, LastRunStatus: AnalysisRunStatus.Cancelled));
                return;
            }
            revoked = grant.Revoked;
        }
        MediaAnalysisPlan? plan = _configuration.Plans.SingleOrDefault(plan => plan.MediaKind == work.MediaKind);
        if (plan == null || plan.Version != work.Run.PlanVersion || !plan.Steps.Select(step => step.Capability).SequenceEqual(work.Run.Steps))
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.Failed, shutdown).ConfigureAwait(false);
            Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, FailureCode: "plan-changed", LastRunStatus: AnalysisRunStatus.Failed));
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown, revoked);
        lock (_activeLock) { _activeCancellation = cancellation; _activeToken = work.Token; }
        CancellationToken ct = cancellation.Token;
        try
        {
            Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, work.Run.CompletedSteps.Count, plan.Steps.Count));
            await using IAnalysisSourceLease source = await OpenSourceAsync(work.SourcePath, ct).ConfigureAwait(false);
            if (work.Run.SourceRevision != null && source.Revision != work.Run.SourceRevision)
            {
                await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
                return;
            }
            using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(ct).ConfigureAwait(false))
            {
                if (!Permits(grant, work) || !await _store.BindSourceAsync(work.Token, source.Revision, ct).ConfigureAwait(false)) return;
            }
            DateTimeOffset? capturedAt = (await _catalog.GetAsync(work.Token.CaptureId, ct).ConfigureAwait(false))?.CapturedAt;
            var input = new AnalysisInput(work.Token.CaptureId, work.MediaKind, source.Revision, source.Path, work.Language, capturedAt);
            for (int index = work.Run.CompletedSteps.Count; index < plan.Steps.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                AnalysisWorkItem? current = await _store.GetWorkAsync(work.Token.CaptureId, ct).ConfigureAwait(false);
                if (current?.Token != work.Token || !current.Run.IsPending) return;
                AnalyzerOutcome outcome = await ExecuteStepAsync(plan.Steps[index], work, input, remaining, index, plan.Steps.Count, ct).ConfigureAwait(false);
                if (outcome.Kind is AnalyzerOutcomeKind.Cancelled or AnalyzerOutcomeKind.InvalidSource)
                {
                    await _store.FinishAsync(work.Token, outcome.Kind == AnalyzerOutcomeKind.Cancelled
                        ? AnalysisRunStatus.Cancelled : AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
                    return;
                }
                if (!await VerifySourceAsync(source, ct).ConfigureAwait(false))
                {
                    await _store.FinishAsync(work.Token, AnalysisRunStatus.InvalidSource, shutdown).ConfigureAwait(false);
                    return;
                }
                using IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(ct).ConfigureAwait(false);
                if (!Permits(grant, work)) return;
                AnalysisResult? result = outcome.Kind == AnalyzerOutcomeKind.Succeeded
                    ? new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, plan.Version, work.Run.Id) : null;
                if (!await _store.CommitStepAsync(work.Token, new(plan.Steps[index].Capability, outcome.Kind, outcome.FailureCode), result, ct).ConfigureAwait(false)) return;
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            await _store.FinishAsync(work.Token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
        }
        catch (ProviderBusyException)
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
            lock (_activeLock) { _activeCancellation = null; _activeToken = null; }
            if (!shutdown.IsCancellationRequested)
            {
                AnalysisWorkItem? finished = await _store.GetWorkAsync(work.Token.CaptureId, shutdown).ConfigureAwait(false);
                if (finished?.Token == work.Token && !finished.Run.IsPending)
                    Report(new(AnalysisActivity.Analyzing, remaining, work.Token.CaptureId, finished.Run.CompletedSteps.Count, plan.Steps.Count,
                        FailureCode: finished.Run.CompletedSteps.LastOrDefault(step => step.Outcome != AnalyzerOutcomeKind.Succeeded)?.FailureCode ?? Progress.FailureCode,
                        LastRunStatus: finished.Run.Status,
                        LastRunHadFailures: finished.Run.Status == AnalysisRunStatus.Completed && finished.Run.CompletedSteps.Any(step =>
                            step.Outcome is AnalyzerOutcomeKind.Failed or AnalyzerOutcomeKind.TemporarilyUnavailable)));
            }
        }
    }

    private async Task<AnalyzerOutcome> ExecuteStepAsync(AnalysisStep step, AnalysisWorkItem work, AnalysisInput input,
        int remaining, int index, int total, CancellationToken ct)
    {
        AnalyzerOutcome last = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "no-compatible-model");
        AnalyzerOutcome strongestFailure = last;
        foreach (string id in step.Candidates)
        for (int retry = 0; retry <= step.RetryCount; retry++)
        {
            IMediaAnalyzer analyzer = _analyzers[id];
            long version = AdvanceProgressVersion();
            var progress = new InlineProgress(value => Report(
                new(value.Stage == AnalysisProgressStage.Preparing ? AnalysisActivity.Preparing : AnalysisActivity.Analyzing,
                    remaining, work.Token.CaptureId, index, total, value.Fraction), version, ct));
            try
            {
                AnalyzerAvailability available = await InvokeAsync(token => analyzer.GetAvailabilityAsync(work.MediaKind, work.Language, token).AsTask(),
                    TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                if (available == AnalyzerAvailability.PreparationRequired)
                {
                    progress.Report(new(AnalysisProgressStage.Preparing));
                    available = await InvokeAsync(token => analyzer.PrepareAsync(progress, token), step.PreparationTimeout, ct).ConfigureAwait(false);
                }
                if (available != AnalyzerAvailability.Ready)
                {
                    last = AnalyzerOutcome.Unsuccessful(available == AnalyzerAvailability.Unsupported
                        ? AnalyzerOutcomeKind.Unsupported : AnalyzerOutcomeKind.TemporarilyUnavailable, "model-unavailable");
                    break;
                }
                progress.Report(new(AnalysisProgressStage.Analyzing));
                last = await InvokeAsync(token => analyzer.AnalyzeAsync(input, progress, token), step.ExecutionTimeout, ct).ConfigureAwait(false);
                if (last.Kind == AnalyzerOutcomeKind.Succeeded && (last.Payload!.Capability != step.Capability ||
                    !last.Payload.Supports(work.MediaKind) || last.Producer!.AnalyzerId != id))
                    last = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "invalid-model-output");
                if (last.Kind is AnalyzerOutcomeKind.Succeeded or AnalyzerOutcomeKind.ContentRejected or AnalyzerOutcomeKind.Cancelled or AnalyzerOutcomeKind.InvalidSource)
                    return last;
                if (last.Kind == AnalyzerOutcomeKind.Unsupported) break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (ProviderBusyException) { throw; }
            catch (TimeoutException) { last = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "model-timeout"); }
            catch (OperationCanceledException) { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Cancelled, "model-cancelled"); }
            catch (Exception) { last = AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "model-failed"); }
            finally
            {
                // An unavailable/unsupported fallback cannot hide an earlier execution failure.
                if (FailureRank(last.Kind) >= FailureRank(strongestFailure.Kind)) strongestFailure = last;
                AdvanceProgressVersion();
            }
        }
        return strongestFailure;
    }

    private static int FailureRank(AnalyzerOutcomeKind kind) => kind switch
    {
        AnalyzerOutcomeKind.Failed => 2,
        AnalyzerOutcomeKind.TemporarilyUnavailable => 1,
        _ => 0,
    };

    private async Task<T> InvokeAsync<T>(Func<CancellationToken, Task<T>> invoke, TimeSpan timeout, CancellationToken ct)
    {
        if (_invocation is { IsCompleted: false }) throw new ProviderBusyException();
        var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<T> task = Task.Run(async () =>
        {
            try { return await invoke(attempt.Token).ConfigureAwait(false); }
            finally { attempt.Dispose(); }
        }, CancellationToken.None);
        _invocation = task;
        try { return await task.WaitAsync(timeout, ct).ConfigureAwait(false); }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            RequestCancellation(attempt);
            try { await task.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false); }
            catch (TimeoutException) { _ = ObserveAsync(task); throw new ProviderBusyException(); }
            catch (Exception) { /* Observe the failed/cancelled invocation before allowing another model. */ }
            throw;
        }
    }

    private static async Task ObserveAsync(Task task) { try { await task.ConfigureAwait(false); } catch (Exception) { } }
    private async Task<IAnalysisSourceLease> OpenSourceAsync(string path, CancellationToken ct)
    {
        try { return await _source.OpenAsync(path, ct).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw new SourceUnavailableException(); }
    }
    private static async Task<bool> VerifySourceAsync(IAnalysisSourceLease source, CancellationToken ct)
    {
        try { return await source.VerifyAsync(ct).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw new SourceUnavailableException(); }
    }
    private static bool Permits(IAnalysisAuthorizationLease grant, AnalysisWorkItem work) =>
        grant.IsAllowed && grant.Revision == work.Run.AuthorizationId && !grant.Revoked.IsCancellationRequested;

    private void CancelActive(AnalysisRunToken? token)
    {
        lock (_activeLock) { if (token == null || token == _activeToken) RequestCancellation(_activeCancellation); }
    }
    private static void RequestCancellation(CancellationTokenSource? source)
    {
        // Revocation is immediate; third-party callbacks must not block or fail a clear/cancel command.
        try { if (source != null) _ = ObserveAsync(source.CancelAsync()); } catch (ObjectDisposedException) { }
    }
    private void Signal() { try { _signal.Release(); } catch (SemaphoreFullException) { } }
    private long AdvanceProgressVersion()
    {
        lock (_progressLock) { return ++_progressVersion; }
    }
    private void Report(AnalysisActivitySnapshot snapshot, long? version = null, CancellationToken ct = default)
    {
        lock (_progressLock)
        {
            if (version != null && (version != _progressVersion || ct.IsCancellationRequested)) return;
            Volatile.Write(ref _progress, snapshot);
            if (_publishingProgress) return;
            _publishingProgress = true;
        }
        // One publisher delivers snapshots in order, coalescing concurrent updates.
        // Observers run outside the state lock and may safely read or change worker state.
        while (true)
        {
            if (ProgressChanged is { } changed)
                foreach (Action<AnalysisActivitySnapshot> observer in changed.GetInvocationList())
                {
                    try { observer(snapshot); } catch (Exception) { /* An observer cannot own or stop application work. */ }
                }
            lock (_progressLock)
            {
                if (ReferenceEquals(snapshot, _progress))
                {
                    _publishingProgress = false;
                    return;
                }
                snapshot = _progress;
            }
        }
    }
    public void Dispose() { _signal.Dispose(); _commands.Dispose(); _running.Dispose(); }
    private sealed class InlineProgress(Action<AnalysisProgress> report) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => report(value);
    }
    private sealed class ProviderBusyException : Exception;
    private sealed class SourceUnavailableException : Exception;
}
