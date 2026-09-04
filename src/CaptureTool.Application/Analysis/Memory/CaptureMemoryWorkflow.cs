using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis.Memory;

/// <summary>
/// One app-owned command workflow for every Capture Memory entry point. Pages observe this
/// service; navigation does not own its cancellation or restart recovery.
/// </summary>
internal sealed class CaptureMemoryWorkflow(
    ICaptureAnalysisPolicyService policy,
    ICaptureAnalysisPolicyCommandService commands,
    ICaptureAnalysisMaintenanceService maintenance,
    IUserInitiatedAnalysisCapabilityPreparationService preparation,
    ICaptureAnalysisBackfillService backfill,
    ICaptureAnalysisCleanupCoordinator cleanup,
    ICaptureMemoryOperationStore operations,
    IClock clock,
    ILogService log,
    ICancellationService? cancellationService = null) : ICaptureMemoryWorkflow, IDisposable
{
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = cancellationService?.GetLinkedCancellationTokenSource() ?? new();
    private readonly object _stateGate = new();
    private ActiveRun? _active;
    private Task<CaptureMemoryOperation>? _runningTask;

    public event EventHandler? Changed;

    public async ValueTask<CaptureMemoryWorkflowSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        CaptureAnalysisPolicySnapshot currentPolicy = await policy.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        CaptureMemoryOperationSnapshot stored = await operations.GetAsync(cancellationToken).ConfigureAwait(false);
        lock (_stateGate)
        {
            CaptureMemoryOperation? operation = stored.Operation;
            if (_active is { ExecutionFinished: true } ended && ended.Operation.Id == operation?.Id &&
                operation.IsRunning && !_shutdown.IsCancellationRequested)
            {
                operation = operation.Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.RecoveryRequired);
            }
            return new(currentPolicy, operation,
                _active?.Operation.Id == stored.Operation?.Id ? _active?.FractionComplete ?? 0 : 0);
        }
    }

    public async Task<CaptureMemoryOperation> ExecuteAsync(CaptureMemoryOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Task<CaptureMemoryOperation> run;
        Guid operationId;
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _shutdown.Token.ThrowIfCancellationRequested();
            CaptureMemoryOperationSnapshot stored = await operations.GetAsync(cancellationToken).ConfigureAwait(false);
            if (stored.Operation?.IsRunning == true && _runningTask is { IsCompleted: true } &&
                request == stored.Operation.Request)
            {
                // A failed final journal write is retryable with the same identity, not a
                // permanently busy page and not a second force-reanalysis operation.
                operationId = stored.Operation.Id;
                run = Start(stored);
            }
            else
            {
            bool superseding = request.Kind == CaptureMemoryOperationKind.TurnOffAndErase;
            if (!superseding && (stored.Operation?.IsRunning == true || _runningTask is { IsCompleted: false }))
            {
                // Never overwrite an accepted intent. Destructive supersession is explicit,
                // not an accidental second page starting a different operation.
                return CreateOperation(request, await policy.GetCurrentAsync(cancellationToken).ConfigureAwait(false))
                    .Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.Conflict);
            }
            CaptureAnalysisPolicySnapshot current = await policy.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            CaptureMemoryOperation operation = CreateOperation(request, current);
            operationId = operation.Id;
            if (!await operations.TryWriteAsync(operation, stored.Revision, cancellationToken).ConfigureAwait(false))
            {
                return operation.Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.Conflict);
            }
            // Persist the replacement before cancellation. The old writer loses its CAS,
            // and revocation need not wait for a provider that ignores cancellation.
            ActiveRun? displaced;
            lock (_stateGate) { displaced = _active; }
            displaced?.CancelByUser();
            run = Start(new(stored.Revision + 1, operation));
            }
        }
        finally { _startGate.Release(); }

        // Caller cancellation is an explicit command cancellation. UI page-lifetime tokens
        // must not be passed here; view disposal only unsubscribes from observation.
        using CancellationTokenRegistration registration = cancellationToken.Register(() => Cancel(operationId));
        return await run.ConfigureAwait(false);
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_runningTask is { IsCompleted: false }) { return; }
            CaptureMemoryOperationSnapshot stored = await operations.GetAsync(cancellationToken).ConfigureAwait(false);
            if (stored.Operation?.IsRunning == true) { _ = Start(stored); }
        }
        finally { _startGate.Release(); }
    }

    public void Cancel(Guid operationId)
    {
        ActiveRun? current;
        lock (_stateGate)
        {
            current = _active?.Operation.Id == operationId ? _active : null;
        }
        current?.CancelByUser();
    }

    public void Dispose() => _shutdown.Cancel();

    private Task<CaptureMemoryOperation> Start(CaptureMemoryOperationSnapshot stored)
    {
        var active = new ActiveRun(stored.Operation!, stored.Revision,
            CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token));
        lock (_stateGate) { _active = active; }
        _runningTask = Task.Run(() => RunAsync(active));
        NotifyChanged();
        return _runningTask;
    }

    private CaptureMemoryOperation CreateOperation(CaptureMemoryOperationRequest request, CaptureAnalysisPolicySnapshot current)
    {
        CaptureId[] targets = request.Kind == CaptureMemoryOperationKind.Reanalyze
            ? current.ControlSnapshot?.State.Enrollments.Where(row => row.CanReanalyze)
                .Select(row => row.CaptureId).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray() ?? []
            : [];
        return new(Guid.NewGuid(), request, new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)),
            current.Policy?.ControlGeneration ?? 0, current.Policy?.PolicyRevision ?? 0,
            CaptureMemoryOperationPhase.Accepted, CaptureMemoryOperationStatus.Running, targets);
    }

    private async Task<CaptureMemoryOperation> RunAsync(ActiveRun active)
    {
        CancellationToken token = active.Cancellation.Token;
        try
        {
            CaptureAnalysisPolicySnapshot current = await policy.GetCurrentAsync(token).ConfigureAwait(false);
            CaptureMemoryOperationKind kind = active.Operation.Request.Kind;
            if (kind == CaptureMemoryOperationKind.TurnOffAndErase)
            {
                await SaveAsync(active, CaptureMemoryOperationPhase.Cleaning, token).ConfigureAwait(false);
                CaptureAnalysisPolicyChangeStatus result = current.IsProcessingAuthorized
                    ? (await commands.RevokeAsync(current.ControlDocumentRevision, token).ConfigureAwait(false)).Status
                    : await cleanup.ReconcileAsync(token).ConfigureAwait(false)
                        ? CaptureAnalysisPolicyChangeStatus.Succeeded : CaptureAnalysisPolicyChangeStatus.ReconciliationRequired;
                return await FinishAsync(active, Map(result)).ConfigureAwait(false);
            }

            if (kind == CaptureMemoryOperationKind.ClearMemory && active.Operation.Phase == CaptureMemoryOperationPhase.Cleaning &&
                current.IsProcessingAuthorized && current.Policy!.ControlGeneration == active.Operation.ControlGeneration + 1 &&
                current.Policy.PolicyRevision == active.Operation.PolicyRevision)
            {
                bool completed = await cleanup.ReconcileAsync(token).ConfigureAwait(false);
                return await FinishAsync(active, completed ? CaptureMemoryOperationStatus.Succeeded :
                    CaptureMemoryOperationStatus.RecoveryRequired).ConfigureAwait(false);
            }

            bool mayCompleteAcceptedGrant = kind == CaptureMemoryOperationKind.Enable &&
                active.Operation.Phase is CaptureMemoryOperationPhase.Accepted or CaptureMemoryOperationPhase.Authorizing;
            if (current.Policy == null || current.Policy.ControlGeneration != active.Operation.ControlGeneration ||
                (!mayCompleteAcceptedGrant && current.Policy.PolicyRevision != active.Operation.PolicyRevision))
            {
                return await FinishAsync(active, CaptureMemoryOperationStatus.Conflict).ConfigureAwait(false);
            }

            if (kind == CaptureMemoryOperationKind.Enable)
            {
                if (!current.IsProcessingAuthorized)
                {
                    // A recovered intent cannot re-grant after any revocation/epoch change.
                    if (!mayCompleteAcceptedGrant || current.Policy.PolicyRevision != active.Operation.PolicyRevision)
                    {
                        return await FinishAsync(active, CaptureMemoryOperationStatus.Rejected).ConfigureAwait(false);
                    }
                    await SaveAsync(active, CaptureMemoryOperationPhase.Authorizing, token).ConfigureAwait(false);
                    CaptureAnalysisPolicyChangeResult consent = await commands.ApplyConsentDecisionAsync(
                        new(CaptureAnalysisPolicyDefaults.CreateConsentDisclosure(), CaptureAnalysisConsentDecision.GrantedForFutureCaptures),
                        current.ControlDocumentRevision, token).ConfigureAwait(false);
                    if (consent.Status != CaptureAnalysisPolicyChangeStatus.Succeeded)
                    {
                        return await FinishAsync(active, Map(consent.Status)).ConfigureAwait(false);
                    }
                    current = consent.Policy;
                }

                if (active.Operation.Phase is not (CaptureMemoryOperationPhase.SchedulingCaptures or CaptureMemoryOperationPhase.AuthorizingBackfill))
                {
                await SaveAsync(active, active.Operation.Advance(CaptureMemoryOperationPhase.PreparingModels,
                    controlGeneration: current.Policy!.ControlGeneration, policyRevision: current.Policy.PolicyRevision), token).ConfigureAwait(false);
                bool limited = await PrepareModelsAsync(active, current.Policy.ProcessingPolicy!, token).ConfigureAwait(false);
                await SaveAsync(active, active.Operation.Advance(CaptureMemoryOperationPhase.AuthorizingBackfill,
                    hasLimitedModelCoverage: limited), token).ConfigureAwait(false);
                }
                if (!active.Operation.Request.IncludeExistingCaptures)
                {
                    return await FinishAsync(active, active.Operation.HasLimitedModelCoverage ? CaptureMemoryOperationStatus.Partial : CaptureMemoryOperationStatus.Succeeded).ConfigureAwait(false);
                }
            }
            else if (!current.IsProcessingAuthorized && kind != CaptureMemoryOperationKind.RebuildSearch)
            {
                return await FinishAsync(active, CaptureMemoryOperationStatus.Rejected).ConfigureAwait(false);
            }

            if (kind is CaptureMemoryOperationKind.Enable or CaptureMemoryOperationKind.IncludeExistingCaptures)
            {
                current = await policy.GetCurrentAsync(token).ConfigureAwait(false);
                if (!MatchesEpoch(active.Operation, current))
                {
                    return await FinishAsync(active, CaptureMemoryOperationStatus.Conflict).ConfigureAwait(false);
                }
                if (active.Operation.Phase != CaptureMemoryOperationPhase.SchedulingCaptures)
                {
                    await SaveAsync(active, CaptureMemoryOperationPhase.AuthorizingBackfill, token).ConfigureAwait(false);
                    var authorization = await commands.AuthorizeExistingCaptureBackfillAsync(current.ControlDocumentRevision, token).ConfigureAwait(false);
                    if (authorization.Status != CaptureAnalysisPolicyChangeStatus.Succeeded)
                    {
                        return await FinishAsync(active, Map(authorization.Status)).ConfigureAwait(false);
                    }
                    await SaveAsync(active, CaptureMemoryOperationPhase.SchedulingCaptures, token).ConfigureAwait(false);
                }
                CaptureAnalysisBackfillRunResult result = await backfill.RunAsync(
                    new InlineProgress<CaptureAnalysisBackfillProgress>(value => Report(active, value.Fraction)), token).ConfigureAwait(false);
                CaptureMemoryOperationStatus status = result.Status switch
                {
                    CaptureAnalysisBackfillRunStatus.Completed or CaptureAnalysisBackfillRunStatus.AlreadyCompleted =>
                        active.Operation.HasLimitedModelCoverage ? CaptureMemoryOperationStatus.Partial : CaptureMemoryOperationStatus.Succeeded,
                    CaptureAnalysisBackfillRunStatus.Cancelled => CaptureMemoryOperationStatus.Cancelled,
                    CaptureAnalysisBackfillRunStatus.Conflict => CaptureMemoryOperationStatus.Conflict,
                    CaptureAnalysisBackfillRunStatus.NotAuthorized => CaptureMemoryOperationStatus.Rejected,
                    _ => CaptureMemoryOperationStatus.Partial,
                };
                return await FinishAsync(active, status, result.Progress.ScheduledCaptureCount,
                    result.Status is CaptureAnalysisBackfillRunStatus.Completed or CaptureAnalysisBackfillRunStatus.AlreadyCompleted).ConfigureAwait(false);
            }

            if (kind is CaptureMemoryOperationKind.StopNewCaptures or CaptureMemoryOperationKind.ResumeNewCaptures)
            {
                var result = kind == CaptureMemoryOperationKind.StopNewCaptures
                    ? await commands.StopFutureCapturesAsync(current.ControlDocumentRevision, token).ConfigureAwait(false)
                    : await commands.ResumeFutureCaptureAdmissionAsync(current.ControlDocumentRevision, token).ConfigureAwait(false);
                return await FinishAsync(active, Map(result.Status)).ConfigureAwait(false);
            }

            if (kind == CaptureMemoryOperationKind.Reanalyze && active.Operation.CaptureIds.Count == 0)
            {
                return await FinishAsync(active, CaptureMemoryOperationStatus.Rejected).ConfigureAwait(false);
            }
            await SaveAsync(active, kind switch
            {
                CaptureMemoryOperationKind.ClearMemory => CaptureMemoryOperationPhase.Cleaning,
                CaptureMemoryOperationKind.RebuildSearch => CaptureMemoryOperationPhase.RebuildingSearch,
                _ => CaptureMemoryOperationPhase.PreparingModels,
            }, token).ConfigureAwait(false);
            CaptureAnalysisMaintenanceResult maintenanceResult = kind switch
            {
                CaptureMemoryOperationKind.ClearMemory => await maintenance.ClearMemoryAsync(token).ConfigureAwait(false),
                CaptureMemoryOperationKind.RebuildSearch => await maintenance.RebuildSearchIndexAsync(token).ConfigureAwait(false),
                _ => await maintenance.ReanalyzeCapturesAsync(new(CaptureAnalysisReanalysisScope.SelectedCaptures,
                        active.Operation.CaptureIds, active.Operation.Id),
                    new InlineProgress<CaptureAnalysisMaintenanceProgress>(value => Report(active, value.FractionComplete)), token).ConfigureAwait(false),
            };
            return await FinishAsync(active, maintenanceResult.Status switch
            {
                CaptureAnalysisMaintenanceStatus.Succeeded => CaptureMemoryOperationStatus.Succeeded,
                CaptureAnalysisMaintenanceStatus.Incomplete => kind == CaptureMemoryOperationKind.ClearMemory
                    ? CaptureMemoryOperationStatus.RecoveryRequired : CaptureMemoryOperationStatus.Partial,
                CaptureAnalysisMaintenanceStatus.Conflict => CaptureMemoryOperationStatus.Conflict,
                CaptureAnalysisMaintenanceStatus.Rejected => CaptureMemoryOperationStatus.Rejected,
                _ => CaptureMemoryOperationStatus.Failed,
            }, maintenanceResult.AffectedCaptureCount).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (_shutdown.IsCancellationRequested && !active.CancelledByUser)
            {
                // Keep the durable intent resumable on shutdown. Explicit cancellation is terminal.
                return active.Operation;
            }
            return await FinishSafelyAsync(active, CaptureMemoryOperationStatus.Cancelled).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            log.LogException(exception, "Capture Memory workflow could not finish.");
            return await FinishSafelyAsync(active, active.CancelledByUser
                ? CaptureMemoryOperationStatus.Cancelled : CaptureMemoryOperationStatus.Failed).ConfigureAwait(false);
        }
        finally
        {
            lock (_stateGate) { active.ExecutionFinished = true; }
            active.Cancellation.Dispose();
            NotifyChanged();
        }
    }

    private async Task<bool> PrepareModelsAsync(ActiveRun active, AnalysisProcessingPolicy processingPolicy, CancellationToken token)
    {
        CaptureAnalysisRecipe[] recipes = [CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe(),
            CaptureAnalysisRecipeDefaults.CreateCaptureMemoryAudioRecipe(), CaptureAnalysisRecipeDefaults.CreateCaptureMemoryVideoRecipe()];
        var capabilities = recipes.SelectMany(recipe => recipe.Capabilities.Select(capability =>
            (recipe.MediaKind, capability.Capability))).Distinct().ToArray();
        bool limited = false;
        for (int index = 0; index < capabilities.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            if (!MatchesEpoch(active.Operation, await policy.GetCurrentAsync(token).ConfigureAwait(false)))
            {
                throw new InvalidOperationException("Capture Memory authorization changed during preparation.");
            }
            var capability = capabilities[index];
            double start = (double)index / capabilities.Length;
            try
            {
                var result = await preparation.PrepareAsync(new(capability.Capability, capability.MediaKind,
                    CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose, processingPolicy),
                    new InlineProgress<AnalysisCapabilityPreparationProgress>(value =>
                        Report(active, start + value.FractionComplete / capabilities.Length)), token).ConfigureAwait(false);
                limited |= result.Status != AnalysisCapabilityPreparationStatus.Ready;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                log.LogException(exception, "A Capture Memory capability could not be prepared.");
                limited = true;
            }
            Report(active, (double)(index + 1) / capabilities.Length);
        }
        return limited;
    }

    private static bool MatchesEpoch(CaptureMemoryOperation operation, CaptureAnalysisPolicySnapshot snapshot) =>
        snapshot.IsProcessingAuthorized && snapshot.Policy?.ControlGeneration == operation.ControlGeneration &&
        snapshot.Policy.PolicyRevision == operation.PolicyRevision;

    private static CaptureMemoryOperationStatus Map(CaptureAnalysisPolicyChangeStatus status) => status switch
    {
        CaptureAnalysisPolicyChangeStatus.Succeeded => CaptureMemoryOperationStatus.Succeeded,
        CaptureAnalysisPolicyChangeStatus.ReconciliationRequired => CaptureMemoryOperationStatus.RecoveryRequired,
        CaptureAnalysisPolicyChangeStatus.Conflict => CaptureMemoryOperationStatus.Conflict,
        CaptureAnalysisPolicyChangeStatus.Rejected => CaptureMemoryOperationStatus.Rejected,
        _ => CaptureMemoryOperationStatus.Failed,
    };

    private Task SaveAsync(ActiveRun active, CaptureMemoryOperationPhase phase, CancellationToken token) =>
        SaveAsync(active, active.Operation.Advance(phase), token);

    private async Task SaveAsync(ActiveRun active, CaptureMemoryOperation operation, CancellationToken token)
    {
        if (!await operations.TryWriteAsync(operation, active.Revision, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Capture Memory operation revision changed.");
        }
        lock (_stateGate) { active.Operation = operation; active.Revision++; active.FractionComplete = 0; }
        NotifyChanged();
    }

    private async Task<CaptureMemoryOperation> FinishAsync(ActiveRun active, CaptureMemoryOperationStatus status,
        int count = 0, bool schedulingComplete = false)
    {
        var completed = active.Operation.Advance(CaptureMemoryOperationPhase.Finished, status,
            affectedCaptureCount: count, isSchedulingComplete: schedulingComplete);
        await SaveAsync(active, completed, CancellationToken.None).ConfigureAwait(false);
        return completed;
    }

    private async Task<CaptureMemoryOperation> FinishSafelyAsync(ActiveRun active, CaptureMemoryOperationStatus status)
    {
        try { return await FinishAsync(active, status).ConfigureAwait(false); }
        catch (Exception exception)
        {
            log.LogException(exception, "Capture Memory operation recovery state could not be saved.");
            // Superseded work can never overwrite the replacement intent.
            if (active.CancelledByUser)
            {
                return active.Operation.Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.Cancelled);
            }
            return active.Operation.Advance(CaptureMemoryOperationPhase.Finished, CaptureMemoryOperationStatus.RecoveryRequired);
        }
    }

    private void Report(ActiveRun active, double fraction)
    {
        lock (_stateGate)
        {
            if (!ReferenceEquals(active, _active) || active.CancelledByUser || !active.Operation.IsRunning) { return; }
            active.FractionComplete = Math.Clamp(fraction, 0, 1);
        }
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (Changed is not { } handlers) { return; }
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception exception) { log.LogException(exception, "Capture Memory observer failed."); }
        }
    }

    private sealed class ActiveRun(CaptureMemoryOperation operation, long revision, CancellationTokenSource cancellation)
    {
        public CaptureMemoryOperation Operation = operation;
        public long Revision = revision;
        public double FractionComplete;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public bool CancelledByUser;
        public bool ExecutionFinished;
        public void CancelByUser()
        {
            if (!Operation.IsRunning || ExecutionFinished) { return; }
            CancelledByUser = true;
            try { Cancellation.Cancel(); } catch (ObjectDisposedException) { }
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
