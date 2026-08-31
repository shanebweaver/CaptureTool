using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Domain;
using CaptureTool.Application.Abstractions.Analysis.Memory;

namespace CaptureTool.Application.Analysis.Activity;

internal sealed class CaptureAnalysisActivityQueryService :
    ICaptureAnalysisActivityQueryService
{
    private static readonly TimeSpan CompletedPreparationVisibility =
        TimeSpan.FromSeconds(2);

    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureMemoryWorkflow _workflow;
    private readonly IAnalysisCapabilityPreparationActivityQueryService
        _preparationActivityQuery;
    private readonly object _preparationGate = new();
    private IReadOnlyList<CaptureAnalysisModelPreparationActivity> _recentPreparations = [];
    private DateTimeOffset _recentPreparationsExpireAtUtc;

    public CaptureAnalysisActivityQueryService(
        ICaptureAnalysisJobStore jobStore,
        ICaptureMemoryWorkflow workflow,
        IAnalysisCapabilityPreparationActivityQueryService preparationActivityQuery)
    {
        ArgumentNullException.ThrowIfNull(jobStore);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(preparationActivityQuery);
        _jobStore = jobStore;
        _workflow = workflow;
        _workflow.Changed += OnWorkflowChanged;
        _preparationActivityQuery = preparationActivityQuery;
        _preparationActivityQuery.ActivityChanged += OnPreparationActivityChanged;
    }

    public event EventHandler? ActivityChanged;

    public async ValueTask<CaptureAnalysisActivitySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CaptureAnalysisModelPreparationActivity> preparations =
            GetVisiblePreparations();
        var running = new HashSet<CaptureId>();
        var queued = new HashSet<CaptureId>();
        var waiting = new HashSet<CaptureId>();
        var retry = new HashSet<CaptureId>();
        var failed = new HashSet<CaptureId>();

        try
        {
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Live model progress remains useful if durable job diagnostics are unavailable.
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

    private IReadOnlyList<CaptureAnalysisModelPreparationActivity> GetVisiblePreparations()
    {
        IReadOnlyList<CaptureAnalysisModelPreparationActivity> current =
            _preparationActivityQuery.GetCurrentPreparations();
        lock (_preparationGate)
        {
            if (current.Count > 0)
            {
                _recentPreparations = current;
                _recentPreparationsExpireAtUtc =
                    DateTimeOffset.UtcNow + CompletedPreparationVisibility;
                return current;
            }

            if (_recentPreparations.Count > 0 &&
                DateTimeOffset.UtcNow < _recentPreparationsExpireAtUtc)
            {
                return _recentPreparations;
            }

            _recentPreparations = [];
            return [];
        }
    }

    private void OnPreparationActivityChanged(object? sender, EventArgs e)
    {
        _ = GetVisiblePreparations();
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => ActivityChanged?.Invoke(this, EventArgs.Empty);

    private async ValueTask<(bool IsInProgress, double Fraction)>
        GetBackfillProgressAsync(CancellationToken cancellationToken)
    {
        try
        {
            CaptureMemoryWorkflowSnapshot snapshot = await _workflow.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            bool isInProgress = snapshot.Policy.IsProcessingAuthorized && snapshot.Operation is
                { IsRunning: true, Phase: CaptureMemoryOperationPhase.SchedulingCaptures } operation &&
                (operation.Request.Kind == CaptureMemoryOperationKind.IncludeExistingCaptures ||
                 operation.Request is { Kind: CaptureMemoryOperationKind.Enable, IncludeExistingCaptures: true });
            return (isInProgress, isInProgress ? snapshot.FractionComplete : 0);
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
