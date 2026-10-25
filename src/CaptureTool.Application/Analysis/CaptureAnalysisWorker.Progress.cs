using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

public sealed partial class CaptureAnalysisWorker
{
    private async Task ReportFinishedRunAsync(AnalysisRunToken token, int remaining, int totalSteps, CancellationToken cancellationToken)
    {
        AnalysisWorkItem? finished = await _store.GetWorkAsync(token.CaptureId, cancellationToken).ConfigureAwait(false);
        if (finished?.Token != token || finished.Run.IsPending)
        {
            return;
        }

        var completions = finished.Run.CompletedSteps;
        string? failureCode = completions.LastOrDefault(step => step.Outcome != AnalyzerOutcomeKind.Succeeded)?.FailureCode;
        bool hadFailures = completions.Any(step => step.Outcome is AnalyzerOutcomeKind.Failed or AnalyzerOutcomeKind.TemporarilyUnavailable);
        Report(new AnalysisActivitySnapshot(
            Activity: AnalysisActivity.Analyzing,
            QueuedCaptures: remaining,
            CaptureId: token.CaptureId,
            CompletedSteps: completions.Count,
            TotalSteps: totalSteps,
            FailureCode: failureCode ?? Progress.FailureCode,
            LastRunStatus: finished.Run.Status,
            LastRunHadFailures: finished.Run.Status == AnalysisRunStatus.Completed && hadFailures));
    }

    private void NotifyResultCommitted(CaptureId captureId, AnalysisCapability capability)
    {
        if (ResultCommitted is not { } committed)
        {
            return;
        }

        foreach (Action<CaptureId, AnalysisCapability> observer in committed.GetInvocationList())
        {
            try
            {
                observer(captureId, capability);
            }
            catch (Exception) { /* An observer cannot own or stop application work. */ }
        }
    }

    private AnalysisProgressScope BeginStepProgress(AnalysisActivitySnapshot step, CancellationToken cancellationToken)
    {
        long version = AdvanceProgressVersion();
        return new AnalysisProgressScope(
            report: progress => Report(step with
            {
                Activity = progress.Stage == AnalysisProgressStage.Preparing ? AnalysisActivity.Preparing : AnalysisActivity.Analyzing,
                Fraction = progress.Fraction,
            }, version, cancellationToken),
            close: () => AdvanceProgressVersion());
    }

    private long AdvanceProgressVersion()
    {
        lock (_progressLock)
        {
            return ++_progressVersion;
        }
    }

    private void Report(AnalysisActivitySnapshot snapshot, long? version = null, CancellationToken ct = default)
    {
        lock (_progressLock)
        {
            if (version != null && (version != _progressVersion || ct.IsCancellationRequested))
            {
                return;
            }

            Volatile.Write(ref _progress, snapshot);
            if (_publishingProgress)
            {
                return;
            }

            _publishingProgress = true;
        }
        // One publisher delivers snapshots in order, coalescing concurrent updates.
        // Observers run outside the state lock and may safely read or change worker state.
        while (true)
        {
            if (ProgressChanged is { } changed)
            {
                foreach (Action<AnalysisActivitySnapshot> observer in changed.GetInvocationList())
                {
                    try
                    {
                        observer(snapshot);
                    }
                    catch (Exception) { /* An observer cannot own or stop application work. */ }
                }
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
}
