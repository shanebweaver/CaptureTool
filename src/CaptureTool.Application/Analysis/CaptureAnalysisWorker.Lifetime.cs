using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed partial class CaptureAnalysisWorker
{
    private async Task WaitForProviderAsync(Task invocation, IReadOnlyList<AnalysisWorkItem> pending, CancellationToken shutdown)
    {
        CancellationToken revoked;
        AnalysisRunToken[] unauthorized;
        using (IAnalysisAuthorizationLease grant = await _authorization.AcquireAsync(shutdown).ConfigureAwait(false))
        {
            unauthorized = pending.Where(work => !IsAuthorized(grant, work)).Select(work => work.Token).ToArray();
            revoked = grant.Revoked;
        }
        // These run revisions cannot become authorized again. Release the policy lease
        // before writing a potentially large backlog; store tokens reject replacements.
        foreach (AnalysisRunToken token in unauthorized)
        {
            await _store.FinishAsync(token, AnalysisRunStatus.Cancelled, shutdown).ConfigureAwait(false);
        }

        pending = await _store.ReadPendingAsync(shutdown).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return;
        }

        Report(new(AnalysisActivity.ProviderUnavailable, pending.Count, FailureCode: "provider-not-stopped"));
        // Wake on provider completion, queue commands, revocation, or shutdown. Cancel the
        // losing signal wait so it cannot consume a later enqueue/clear notification.
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(shutdown, revoked);
        Task signal = _signal.WaitAsync(wake.Token);
        try
        {
            await Task.WhenAny(invocation, signal).ConfigureAwait(false);
        }
        finally
        {
            await wake.CancelAsync().ConfigureAwait(false);
            try
            {
                await signal.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (wake.IsCancellationRequested) { }
        }
        shutdown.ThrowIfCancellationRequested();
    }

    private async Task<IAnalysisSourceLease> OpenSourceAsync(AnalysisWorkItem work, string? relocatedPath, CancellationToken ct)
    {
        if (work.SourceIdentity == null)
        {
            throw new SourceUnavailableException();
        }

        try
        {
            // A selected copy may still exist after another Save As. Either candidate must
            // identify the queued file; a replacement at the old path is never analyzed.
            try
            {
                return await _source.OpenAsync(work.SourcePath, ct, work.SourceIdentity).ConfigureAwait(false);
            }
            catch (Exception exception) when ((exception is IOException or InvalidDataException or UnauthorizedAccessException) &&
                relocatedPath != null && !string.Equals(work.SourcePath, relocatedPath, StringComparison.OrdinalIgnoreCase))
            {
                return await _source.OpenAsync(relocatedPath, ct, work.SourceIdentity).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new SourceUnavailableException();
        }
    }

    private static async Task<bool> VerifySourceAsync(IAnalysisSourceLease source, CancellationToken ct)
    {
        try
        {
            return await source.VerifyAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new SourceUnavailableException();
        }
    }

    private void CancelActive(AnalysisRunToken? token)
    {
        lock (_activeLock)
        {
            if (token == null || token == _activeToken)
            {
                AnalysisProviderSession.RequestCancellation(_activeCancellation);
            }
        }
    }
}
