using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Privacy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Maintenance;

internal sealed class CaptureAnalysisLifecycleService :
    ICaptureAnalysisExclusionService,
    ICaptureAnalysisMaintenanceService,
    ICaptureAssetRemovalService,
    IDisposable
{
    private const int MaximumControlWriteAttempts = 4;

    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly ICaptureAnalysisCleanupCoordinator _cleanup;
    private readonly ICaptureAnalysisProjectionMaintenance _projectionMaintenance;
    private readonly IUserInitiatedAnalysisCapabilityPreparationService _preparation;
    private readonly ICaptureAnalysisScheduler _scheduler;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    public CaptureAnalysisLifecycleService(
        ICaptureAnalysisControlStore controlStore,
        ICaptureAssetCatalog captureAssets,
        ICaptureAnalysisCleanupCoordinator cleanup,
        ICaptureAnalysisProjectionMaintenance projectionMaintenance,
        IUserInitiatedAnalysisCapabilityPreparationService preparation,
        ICaptureAnalysisScheduler scheduler)
    {
        _controlStore = controlStore;
        _captureAssets = captureAssets;
        _cleanup = cleanup;
        _projectionMaintenance = projectionMaintenance;
        _preparation = preparation;
        _scheduler = scheduler;
    }

    public async ValueTask<CaptureAnalysisExclusionResult> ExcludeAsync(
        CaptureAnalysisExclusionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureAnalysisExclusionReason reason = request.Kind switch
        {
            CaptureAnalysisExclusionKind.UserExcluded =>
                CaptureAnalysisExclusionReason.UserExcluded,
            CaptureAnalysisExclusionKind.PrivateCapture =>
                CaptureAnalysisExclusionReason.PrivateCapture,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
            {
                CaptureAnalysisControlSnapshot current = await _controlStore
                    .GetAsync(cancellationToken).ConfigureAwait(false);
                CaptureAnalysisEnrollment? existing = FindEnrollment(current.State, request.CaptureId);
                if (existing?.State == CaptureAnalysisEnrollmentState.Forgotten)
                {
                    return new(CaptureAnalysisExclusionStatus.Rejected, request);
                }

                if (existing?.State == CaptureAnalysisEnrollmentState.Excluded)
                {
                    _ = await _cleanup.ReconcileCaptureAsync(request.CaptureId, cancellationToken)
                        .ConfigureAwait(false);
                    return new(CaptureAnalysisExclusionStatus.AlreadyExcluded, request);
                }

                long finalizationSequence = existing?.AssetFinalizationSequence ??
                    FindFinalization(request.CaptureId)?.Sequence ?? 0;
                if (finalizationSequence <= 0)
                {
                    return new(CaptureAnalysisExclusionStatus.Rejected, request);
                }

                CaptureAnalysisEnrollment tombstone = CreateTombstone(
                    request.CaptureId,
                    CaptureAnalysisEnrollmentState.Excluded,
                    reason,
                    finalizationSequence,
                    existing);
                CaptureAnalysisControlWriteResult write = await WriteEnrollmentAsync(
                    current,
                    tombstone,
                    cancellationToken).ConfigureAwait(false);
                if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
                {
                    _ = await _cleanup.ReconcileCaptureAsync(request.CaptureId, cancellationToken)
                        .ConfigureAwait(false);
                    return new(CaptureAnalysisExclusionStatus.Succeeded, request);
                }

                if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
                {
                    return new(CaptureAnalysisExclusionStatus.Unavailable, request);
                }
            }

            return new(CaptureAnalysisExclusionStatus.Conflict, request);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(CaptureAnalysisExclusionStatus.Unavailable, request);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask<CaptureAssetRemovalResult> RemoveAsync(
        CaptureAssetRemovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind == CaptureAssetRemovalKind.DeleteRetainedSource)
        {
            if (!request.IsConfirmed)
            {
                return new(CaptureAssetRemovalStatus.ConfirmationRequired, request);
            }
        }

        CaptureAnalysisExclusionReason removalReason = request.Kind ==
            CaptureAssetRemovalKind.DeleteRetainedSource
                ? CaptureAnalysisExclusionReason.DeleteRequested
                : CaptureAnalysisExclusionReason.HistoryForgotten;

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
            {
                CaptureAnalysisControlSnapshot current = await _controlStore
                    .GetAsync(cancellationToken).ConfigureAwait(false);
                CaptureAnalysisEnrollment? existing = FindEnrollment(current.State, request.CaptureId);
                if (existing is
                    {
                        State: CaptureAnalysisEnrollmentState.Forgotten,
                    } && existing.ExclusionReason == removalReason)
                {
                    bool retried = await _cleanup.ReconcileCaptureAsync(
                        request.CaptureId,
                        cancellationToken).ConfigureAwait(false);
                    return new(
                        retried
                            ? CaptureAssetRemovalStatus.AlreadyRemoved
                            : CaptureAssetRemovalStatus.Incomplete,
                        request);
                }

                if (existing?.State == CaptureAnalysisEnrollmentState.Forgotten)
                {
                    return new(CaptureAssetRemovalStatus.Conflict, request);
                }

                if (request.Kind == CaptureAssetRemovalKind.DeleteRetainedSource)
                {
                    CaptureAsset? asset;
                    try
                    {
                        asset = _captureAssets.Get(request.CaptureId);
                    }
                    catch
                    {
                        return new(CaptureAssetRemovalStatus.Unavailable, request);
                    }

                    if (asset == null)
                    {
                        return new(CaptureAssetRemovalStatus.NotFound, request);
                    }

                    if (asset.SourceOwnership != CaptureSourceOwnership.AppOwned)
                    {
                        return new(CaptureAssetRemovalStatus.OwnershipDenied, request);
                    }
                }

                long finalizationSequence = existing?.AssetFinalizationSequence ??
                    FindFinalization(request.CaptureId)?.Sequence ?? 0;
                if (finalizationSequence <= 0)
                {
                    return new(CaptureAssetRemovalStatus.NotFound, request);
                }

                CaptureAnalysisEnrollment tombstone = CreateTombstone(
                    request.CaptureId,
                    CaptureAnalysisEnrollmentState.Forgotten,
                    removalReason,
                    finalizationSequence,
                    existing);
                CaptureAnalysisControlWriteResult write = await WriteEnrollmentAsync(
                    current,
                    tombstone,
                    cancellationToken).ConfigureAwait(false);
                if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
                {
                    bool completed = await _cleanup.ReconcileCaptureAsync(
                        request.CaptureId,
                        cancellationToken).ConfigureAwait(false);
                    return new(
                        completed
                            ? CaptureAssetRemovalStatus.Succeeded
                            : CaptureAssetRemovalStatus.Incomplete,
                        request);
                }

                if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
                {
                    return new(CaptureAssetRemovalStatus.Unavailable, request);
                }
            }

            return new(CaptureAssetRemovalStatus.Conflict, request);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(CaptureAssetRemovalStatus.Unavailable, request);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisMaintenanceResult> ClearMemoryAsync(
        CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long latestAssetSequence = _captureAssets.GetLatestChangeSequence();
            for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
            {
                CaptureAnalysisControlSnapshot current = await _controlStore
                    .GetAsync(cancellationToken).ConfigureAwait(false);
                if (!current.State.Policy.IsProcessingAuthorized)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Rejected);
                }

                CaptureAnalysisEnrollment[] retired = current.State.Enrollments
                    .Select(RetireEnrollment)
                    .ToArray();
                int affected = current.State.Enrollments.Count(enrollment =>
                    enrollment.State == CaptureAnalysisEnrollmentState.Enrolled);
                var next = new CaptureAnalysisControlState(
                    current.State.Policy.ClearMemory(latestAssetSequence),
                    retired,
                    current.State.CaptureChangeCheckpoint);
                CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                    next,
                    current.DocumentRevision,
                    cancellationToken).ConfigureAwait(false);
                if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
                {
                    bool completed = await _cleanup.ReconcileAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return new(
                        completed
                            ? CaptureAnalysisMaintenanceStatus.Succeeded
                            : CaptureAnalysisMaintenanceStatus.Incomplete,
                        affected);
                }

                if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Unavailable);
                }
            }

            return new(CaptureAnalysisMaintenanceStatus.Conflict);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(CaptureAnalysisMaintenanceStatus.Unavailable);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisMaintenanceResult> RebuildSearchIndexAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            int rebuilt = await _projectionMaintenance.RebuildAsync(cancellationToken)
                .ConfigureAwait(false);
            return new(CaptureAnalysisMaintenanceStatus.Succeeded, rebuilt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(CaptureAnalysisMaintenanceStatus.Unavailable);
        }
    }

    public ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
        CaptureAnalysisReanalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        return ReanalyzeCapturesCoreAsync(request, progress: null, cancellationToken);
    }

    public ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesAsync(
        CaptureAnalysisReanalysisRequest request,
        IProgress<CaptureAnalysisMaintenanceProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return ReanalyzeCapturesCoreAsync(request, progress, cancellationToken);
    }

    private async ValueTask<CaptureAnalysisMaintenanceResult> ReanalyzeCapturesCoreAsync(
        CaptureAnalysisReanalysisRequest request,
        IProgress<CaptureAnalysisMaintenanceProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            CaptureAnalysisControlSnapshot control = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            if (!control.State.Policy.IsProcessingAuthorized ||
                control.State.AuthorizedPurpose is not AnalysisPurpose purpose ||
                control.State.ProcessingPolicy is not AnalysisProcessingPolicy processingPolicy)
            {
                return new(CaptureAnalysisMaintenanceStatus.Rejected);
            }

            CaptureAnalysisEnrollment[] enrollments = control.State.Enrollments
                .Where(enrollment => enrollment.State == CaptureAnalysisEnrollmentState.Enrolled &&
                    (request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures ||
                     request.CaptureIds.Contains(enrollment.CaptureId)))
                .ToArray();
            if (enrollments.Length == 0)
            {
                return new(CaptureAnalysisMaintenanceStatus.Rejected);
            }

            CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults
                .CreateCaptureMemoryImageRecipe();
            ProcessingBoundary? boundary = null;
            progress?.Report(new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.PreparingModels,
                0));
            for (int index = 0; index < recipe.Capabilities.Count; index++)
            {
                RecipeCapability capability = recipe.Capabilities[index];
                double capabilityStart = (double)index / recipe.Capabilities.Count;
                double capabilityShare = 1d / recipe.Capabilities.Count;
                IProgress<AnalysisCapabilityPreparationProgress>? capabilityProgress = progress == null
                    ? null
                    : new DelegateProgress<AnalysisCapabilityPreparationProgress>(value =>
                        progress.Report(new CaptureAnalysisMaintenanceProgress(
                            CaptureAnalysisMaintenancePhase.PreparingModels,
                            (capabilityStart + (value.FractionComplete * capabilityShare)) * 0.5)));
                AnalysisCapabilityPreparationState prepared = await _preparation.PrepareAsync(
                    new AnalysisCapabilityPreparationRequest(
                        capability.Capability,
                        recipe.MediaKind,
                        purpose,
                        processingPolicy),
                    capabilityProgress,
                    cancellationToken).ConfigureAwait(false);
                bool optionalUnavailable =
                    capability.Requirement == RecipeCapabilityRequirement.Optional &&
                    prepared.Status is (AnalysisCapabilityPreparationStatus.Unsupported or
                        AnalysisCapabilityPreparationStatus.Disabled or
                        AnalysisCapabilityPreparationStatus.Failed);
                if (optionalUnavailable)
                {
                    progress?.Report(new CaptureAnalysisMaintenanceProgress(
                        CaptureAnalysisMaintenancePhase.PreparingModels,
                        ((double)(index + 1) / recipe.Capabilities.Count) * 0.5));
                    continue;
                }

                if (prepared.Status != AnalysisCapabilityPreparationStatus.Ready ||
                    prepared.ProcessingBoundary is not ProcessingBoundary preparedBoundary ||
                    (boundary.HasValue && boundary.Value != preparedBoundary))
                {
                    return new(CaptureAnalysisMaintenanceStatus.Incomplete);
                }

                boundary = preparedBoundary;
                progress?.Report(new CaptureAnalysisMaintenanceProgress(
                    CaptureAnalysisMaintenancePhase.PreparingModels,
                    ((double)(index + 1) / recipe.Capabilities.Count) * 0.5));
            }

            int scheduled = 0;
            int requestedCaptureCount = request.Scope ==
                CaptureAnalysisReanalysisScope.SelectedCaptures
                ? request.CaptureIds.Count
                : enrollments.Length;
            progress?.Report(new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.SchedulingCaptures,
                0.5));
            for (int index = 0; index < enrollments.Length; index++)
            {
                CaptureAnalysisEnrollment enrollment = enrollments[index];
                CaptureAssetChange? finalization = FindFinalization(enrollment.CaptureId);
                CaptureAsset? asset = _captureAssets.Get(enrollment.CaptureId);
                if (finalization == null ||
                    asset is not { LifecycleState: CaptureAssetLifecycleState.Active } ||
                    enrollment.RequestedRecipeId != recipe.Id ||
                    enrollment.RequestedRecipeVersion != recipe.Version)
                {
                    ReportSchedulingProgress(progress, index, enrollments.Length);
                    continue;
                }

                var admission = new CaptureAnalysisAdmissionRequest(
                    finalization.Value,
                    purpose,
                    CaptureAnalysisAdmissionKind.FutureCapture);
                CaptureAnalysisScheduleResult result = await _scheduler.ScheduleAsync(
                    new CaptureAnalysisScheduleRequest(
                        admission,
                        recipe,
                        boundary!.Value,
                        forceReanalysis: true),
                    cancellationToken).ConfigureAwait(false);
                if (result.Status is CaptureAnalysisScheduleStatus.Scheduled or
                    CaptureAnalysisScheduleStatus.AlreadyScheduled)
                {
                    scheduled++;
                }

                ReportSchedulingProgress(progress, index, enrollments.Length);
            }

            return new(
                scheduled == requestedCaptureCount
                    ? CaptureAnalysisMaintenanceStatus.Succeeded
                    : CaptureAnalysisMaintenanceStatus.Incomplete,
                scheduled);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(CaptureAnalysisMaintenanceStatus.Unavailable);
        }
    }

    private static void ReportSchedulingProgress(
        IProgress<CaptureAnalysisMaintenanceProgress>? progress,
        int completedIndex,
        int captureCount)
    {
        progress?.Report(new CaptureAnalysisMaintenanceProgress(
            CaptureAnalysisMaintenancePhase.SchedulingCaptures,
            0.5 + (0.5 * (completedIndex + 1) / captureCount)));
    }

    private ValueTask<CaptureAnalysisControlWriteResult> WriteEnrollmentAsync(
        CaptureAnalysisControlSnapshot current,
        CaptureAnalysisEnrollment replacement,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CaptureAnalysisEnrollment> enrollments =
            FindEnrollment(current.State, replacement.CaptureId) == null
                ? [.. current.State.Enrollments, replacement]
                : current.State.Enrollments.Select(enrollment =>
                    enrollment.CaptureId == replacement.CaptureId
                        ? replacement
                        : enrollment).ToArray();
        return _controlStore.TryWriteAsync(
            new CaptureAnalysisControlState(
                current.State.Policy,
                enrollments,
                current.State.CaptureChangeCheckpoint),
            current.DocumentRevision,
            cancellationToken);
    }

    private CaptureAssetChange? FindFinalization(CaptureId captureId)
    {
        CaptureAssetChange change = _captureAssets.GetChangesAfter(0).FirstOrDefault(candidate =>
            candidate.CaptureId == captureId &&
            candidate.ChangeType == CaptureAssetChangeType.Finalized);
        return change.Sequence > 0 ? change : null;
    }

    private static CaptureAnalysisEnrollment? FindEnrollment(
        CaptureAnalysisControlState state,
        CaptureId captureId)
    {
        return state.Enrollments.FirstOrDefault(enrollment => enrollment.CaptureId == captureId);
    }

    private static CaptureAnalysisEnrollment CreateTombstone(
        CaptureId captureId,
        CaptureAnalysisEnrollmentState state,
        CaptureAnalysisExclusionReason reason,
        long finalizationSequence,
        CaptureAnalysisEnrollment? existing)
    {
        return new(
            captureId,
            state,
            reason,
            checked((existing?.EnrollmentGeneration ?? 0) + 1),
            checked((existing?.TombstoneGeneration ?? 0) + 1),
            finalizationSequence,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
    }

    private static CaptureAnalysisEnrollment RetireEnrollment(
        CaptureAnalysisEnrollment enrollment)
    {
        return enrollment.State == CaptureAnalysisEnrollmentState.Enrolled
            ? CreateTombstone(
                enrollment.CaptureId,
                CaptureAnalysisEnrollmentState.Excluded,
                CaptureAnalysisExclusionReason.UserExcluded,
                enrollment.AssetFinalizationSequence,
                enrollment)
            : enrollment;
    }

    public void Dispose()
    {
        _mutationGate.Dispose();
    }

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
