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
                .Where(enrollment => IsReanalyzable(enrollment) &&
                    (request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures ||
                     request.CaptureIds.Contains(enrollment.CaptureId)))
                .ToArray();
            if (enrollments.Length == 0)
            {
                return new(CaptureAnalysisMaintenanceStatus.Rejected);
            }

            if (enrollments.Any(IsClearedEnrollment))
            {
                ReanalysisEnrollmentRestoreResult restored = await RestoreClearedEnrollmentsAsync(
                    request,
                    cancellationToken).ConfigureAwait(false);
                if (restored.Status != CaptureAnalysisMaintenanceStatus.Succeeded)
                {
                    return new(restored.Status);
                }

                if (restored.Control == null)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Unavailable);
                }

                enrollments = restored.Control.State.Enrollments
                    .Where(enrollment =>
                        enrollment.State == CaptureAnalysisEnrollmentState.Enrolled &&
                        (request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures ||
                         request.CaptureIds.Contains(enrollment.CaptureId)))
                    .ToArray();
                if (enrollments.Length == 0)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Rejected);
                }
            }

            ReanalysisWorkItem[] workItems = enrollments
                .Select(TryCreateReanalysisWorkItem)
                .Where(item => item != null)
                .Cast<ReanalysisWorkItem>()
                .ToArray();
            if (workItems.Length == 0)
            {
                return new(CaptureAnalysisMaintenanceStatus.Incomplete);
            }

            RecipePreparation[] preparations = workItems
                .SelectMany(item => item.Recipe.Capabilities.Select(capability =>
                    new RecipePreparation(
                        item.Recipe.MediaKind,
                        capability.Capability,
                        capability.Requirement)))
                .Distinct()
                .ToArray();
            ProcessingBoundary? boundary = null;
            bool preparationIncomplete = false;
            progress?.Report(new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.PreparingModels,
                0));
            for (int index = 0; index < preparations.Length; index++)
            {
                RecipePreparation preparation = preparations[index];
                double capabilityStart = (double)index / preparations.Length;
                double capabilityShare = 1d / preparations.Length;
                IProgress<AnalysisCapabilityPreparationProgress>? capabilityProgress = progress == null
                    ? null
                    : new DelegateProgress<AnalysisCapabilityPreparationProgress>(value =>
                        progress.Report(new CaptureAnalysisMaintenanceProgress(
                            CaptureAnalysisMaintenancePhase.PreparingModels,
                            (capabilityStart + (value.FractionComplete * capabilityShare)) * 0.5)));
                AnalysisCapabilityPreparationState prepared = await _preparation.PrepareAsync(
                    new AnalysisCapabilityPreparationRequest(
                        preparation.Capability,
                        preparation.MediaKind,
                        purpose,
                        processingPolicy),
                    capabilityProgress,
                    cancellationToken).ConfigureAwait(false);
                bool optionalUnavailable =
                    preparation.Requirement == RecipeCapabilityRequirement.Optional &&
                    prepared.Status is (AnalysisCapabilityPreparationStatus.Unsupported or
                        AnalysisCapabilityPreparationStatus.Disabled or
                        AnalysisCapabilityPreparationStatus.Failed);
                if (!optionalUnavailable &&
                    (prepared.Status != AnalysisCapabilityPreparationStatus.Ready ||
                     prepared.ProcessingBoundary is not ProcessingBoundary preparedBoundary ||
                     (boundary.HasValue && boundary.Value != preparedBoundary)))
                {
                    preparationIncomplete = true;
                }
                else if (prepared.ProcessingBoundary is ProcessingBoundary currentBoundary)
                {
                    boundary ??= currentBoundary;
                }

                progress?.Report(new CaptureAnalysisMaintenanceProgress(
                    CaptureAnalysisMaintenancePhase.PreparingModels,
                    ((double)(index + 1) / preparations.Length) * 0.5));
            }

            if (preparationIncomplete || !boundary.HasValue)
            {
                return new(CaptureAnalysisMaintenanceStatus.Incomplete);
            }

            int scheduled = 0;
            int requestedCaptureCount = request.Scope ==
                CaptureAnalysisReanalysisScope.SelectedCaptures
                ? request.CaptureIds.Count
                : enrollments.Length;
            progress?.Report(new CaptureAnalysisMaintenanceProgress(
                CaptureAnalysisMaintenancePhase.SchedulingCaptures,
                0.5));
            for (int index = 0; index < workItems.Length; index++)
            {
                ReanalysisWorkItem item = workItems[index];
                var admission = new CaptureAnalysisAdmissionRequest(
                    item.Finalization,
                    purpose,
                    CaptureAnalysisAdmissionKind.FutureCapture);
                CaptureAnalysisScheduleResult result = await _scheduler.ScheduleAsync(
                    new CaptureAnalysisScheduleRequest(
                        admission,
                        item.Recipe,
                        boundary!.Value,
                        forceReanalysis: true),
                    cancellationToken).ConfigureAwait(false);
                if (result.Status is CaptureAnalysisScheduleStatus.Scheduled or
                    CaptureAnalysisScheduleStatus.AlreadyScheduled)
                {
                    scheduled++;
                }

                ReportSchedulingProgress(progress, index, workItems.Length);
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

    private async ValueTask<ReanalysisEnrollmentRestoreResult> RestoreClearedEnrollmentsAsync(
        CaptureAnalysisReanalysisRequest request,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
            {
                CaptureAnalysisControlSnapshot current = await _controlStore
                    .GetAsync(cancellationToken).ConfigureAwait(false);
                if (!current.State.Policy.IsProcessingAuthorized)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Rejected);
                }

                CaptureAnalysisEnrollment[] cleared = current.State.Enrollments
                    .Where(enrollment => IsClearedEnrollment(enrollment) &&
                        (request.Scope == CaptureAnalysisReanalysisScope.AllEnrolledCaptures ||
                         request.CaptureIds.Contains(enrollment.CaptureId)))
                    .ToArray();
                if (cleared.Length == 0)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Succeeded, current);
                }

                // Keep the privacy fence and tombstones in place until every app-owned derived
                // artifact has been removed. Only the user's confirmed reanalysis action may
                // convert a cleared enrollment back into active work.
                if (!await _cleanup.ReconcileAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new(CaptureAnalysisMaintenanceStatus.Incomplete);
                }

                Dictionary<CaptureId, CaptureAnalysisRecipe> recipes = cleared
                    .Select(enrollment => new
                    {
                        enrollment.CaptureId,
                        Recipe = TryGetCaptureMemoryRecipe(enrollment.CaptureId),
                    })
                    .Where(item => item.Recipe != null)
                    .ToDictionary(item => item.CaptureId, item => item.Recipe!);
                CaptureAnalysisEnrollment[] restored = current.State.Enrollments
                    .Select(enrollment => recipes.TryGetValue(
                        enrollment.CaptureId,
                        out CaptureAnalysisRecipe? recipe)
                        ? RestoreEnrollment(enrollment, recipe)
                        : enrollment)
                    .ToArray();
                var next = new CaptureAnalysisControlState(
                    current.State.Policy,
                    restored,
                    current.State.CaptureChangeCheckpoint);
                CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                    next,
                    current.DocumentRevision,
                    cancellationToken).ConfigureAwait(false);
                if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Succeeded, write.Snapshot);
                }

                if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
                {
                    return new(CaptureAnalysisMaintenanceStatus.Unavailable);
                }
            }

            return new(CaptureAnalysisMaintenanceStatus.Conflict);
        }
        finally
        {
            _mutationGate.Release();
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

    private ReanalysisWorkItem? TryCreateReanalysisWorkItem(CaptureAnalysisEnrollment enrollment)
    {
        CaptureAnalysisRecipe? recipe = TryGetCaptureMemoryRecipe(enrollment.CaptureId);
        CaptureAssetChange? finalization = FindFinalization(enrollment.CaptureId);
        return recipe == null || finalization == null ||
            enrollment.RequestedRecipeId != recipe.Id ||
            enrollment.RequestedRecipeVersion != recipe.Version
                ? null
                : new ReanalysisWorkItem(enrollment, recipe, finalization.Value);
    }

    private CaptureAnalysisRecipe? TryGetCaptureMemoryRecipe(CaptureId captureId)
    {
        CaptureAsset? asset = _captureAssets.Get(captureId);
        if (asset is not
            {
                LifecycleState: CaptureAssetLifecycleState.Active,
                SourceOwnership: CaptureSourceOwnership.AppOwned,
            })
        {
            return null;
        }

        CaptureMediaKind mediaKind = asset.MediaType switch
        {
            CaptureFileType.Image => CaptureMediaKind.Image,
            CaptureFileType.Audio => CaptureMediaKind.Audio,
            CaptureFileType.Video => CaptureMediaKind.Video,
            _ => CaptureMediaKind.Unknown,
        };
        return CaptureAnalysisRecipeDefaults.TryCreateCaptureMemoryRecipe(mediaKind, out CaptureAnalysisRecipe? recipe)
            ? recipe
            : null;
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
                CaptureAnalysisExclusionReason.MemoryCleared,
                enrollment.AssetFinalizationSequence,
                enrollment)
            : enrollment;
    }

    private static bool IsReanalyzable(CaptureAnalysisEnrollment enrollment)
    {
        return enrollment.State == CaptureAnalysisEnrollmentState.Enrolled ||
            IsClearedEnrollment(enrollment);
    }

    private static bool IsClearedEnrollment(CaptureAnalysisEnrollment enrollment)
    {
        return enrollment is
        {
            State: CaptureAnalysisEnrollmentState.Excluded,
            ExclusionReason: CaptureAnalysisExclusionReason.MemoryCleared,
        };
    }

    private static CaptureAnalysisEnrollment RestoreEnrollment(
        CaptureAnalysisEnrollment enrollment,
        CaptureAnalysisRecipe recipe)
    {
        return new(
            enrollment.CaptureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            checked(enrollment.EnrollmentGeneration + 1),
            enrollment.TombstoneGeneration,
            enrollment.AssetFinalizationSequence,
            recipe.Id,
            recipe.Version);
    }

    public void Dispose()
    {
        _mutationGate.Dispose();
    }

    private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed record ReanalysisEnrollmentRestoreResult(
        CaptureAnalysisMaintenanceStatus Status,
        CaptureAnalysisControlSnapshot? Control = null);

    private sealed record ReanalysisWorkItem(
        CaptureAnalysisEnrollment Enrollment,
        CaptureAnalysisRecipe Recipe,
        CaptureAssetChange Finalization);

    private readonly record struct RecipePreparation(
        CaptureMediaKind MediaKind,
        CapabilityDefinition Capability,
        RecipeCapabilityRequirement Requirement);
}
