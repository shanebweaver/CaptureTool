using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Activity;

internal sealed class CaptureAnalysisActivityQueryService :
    ICaptureAnalysisActivityQueryService
{
    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisPolicyService _policyService;
    private readonly IAnalysisCapabilityPreparationActivityQueryService
        _preparationActivityQuery;

    public CaptureAnalysisActivityQueryService(
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisPolicyService policyService,
        IAnalysisCapabilityPreparationActivityQueryService preparationActivityQuery)
    {
        ArgumentNullException.ThrowIfNull(jobStore);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(preparationActivityQuery);
        _jobStore = jobStore;
        _policyService = policyService;
        _preparationActivityQuery = preparationActivityQuery;
    }

    public async ValueTask<CaptureAnalysisActivitySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CaptureAnalysisModelPreparationActivity> preparations =
            _preparationActivityQuery.GetCurrentPreparations();
        var running = new HashSet<CaptureId>();
        var queued = new HashSet<CaptureId>();
        var waiting = new HashSet<CaptureId>();
        var retry = new HashSet<CaptureId>();
        var failed = new HashSet<CaptureId>();

        await foreach (CaptureAnalysisJobIntent job in _jobStore
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            HashSet<CaptureId>? target = job.State switch
            {
                CaptureAnalysisJobState.Running => running,
                CaptureAnalysisJobState.Pending => queued,
                CaptureAnalysisJobState.WaitingForCapability => waiting,
                CaptureAnalysisJobState.RetryScheduled => retry,
                CaptureAnalysisJobState.TerminalFailure => failed,
                _ => null,
            };
            target?.Add(job.Key.CaptureId);
        }

        queued.ExceptWith(running);
        waiting.ExceptWith(running);
        waiting.ExceptWith(queued);
        retry.ExceptWith(running);
        retry.ExceptWith(queued);
        retry.ExceptWith(waiting);

        (bool isBackfillInProgress, double backfillFraction) =
            await GetBackfillProgressAsync(cancellationToken).ConfigureAwait(false);
        return new CaptureAnalysisActivitySnapshot(
            preparations,
            running.Count,
            queued.Count,
            waiting.Count,
            retry.Count,
            failed.Count,
            isBackfillInProgress,
            backfillFraction);
    }

    private async ValueTask<(bool IsInProgress, double Fraction)>
        GetBackfillProgressAsync(CancellationToken cancellationToken)
    {
        try
        {
            CaptureAnalysisPolicySnapshot snapshot = await _policyService
                .GetCurrentAsync(cancellationToken)
                .ConfigureAwait(false);
            CaptureAnalysisPolicy? policy = snapshot.Policy;
            bool isInProgress = snapshot.IsProcessingAuthorized &&
                policy?.BackfillState is CaptureAnalysisBackfillState.Authorized or
                    CaptureAnalysisBackfillState.InProgress;
            if (!isInProgress || policy == null)
            {
                return (false, 0);
            }

            double fraction = policy.BackfillUpperSequence > 0
                ? Math.Clamp(
                    (double)policy.BackfillCheckpoint / policy.BackfillUpperSequence,
                    0,
                    1)
                : 0;
            return (true, fraction);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Job activity remains useful when advisory policy progress is unavailable.
            return (false, 0);
        }
    }
}
