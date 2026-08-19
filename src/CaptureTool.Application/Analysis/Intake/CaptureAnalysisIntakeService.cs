using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Intake;

internal sealed class CaptureAnalysisIntakeService :
    ICaptureAnalysisReconciler,
    ICaptureAnalysisBackfillService,
    IDisposable
{
    private const int MaximumControlWriteAttempts = 4;

    private readonly ICaptureAssetChangeReader _changeReader;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAnalysisPolicyService _policyService;
    private readonly ICaptureAnalysisScheduler _scheduler;
    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisProjectionRefresher _projectionRefresher;
    private readonly ICaptureAnalysisFeatureAvailability _featureAvailability;
    private readonly IFileSystem _fileSystem;
    private readonly ICaptureAnalysisCleanupCoordinator? _cleanup;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _startupAuditCompleted;

    public CaptureAnalysisIntakeService(
        ICaptureAssetChangeReader changeReader,
        ICaptureAssetCatalog captureAssets,
        ICaptureAnalysisControlStore controlStore,
        ICaptureAnalysisPolicyService policyService,
        ICaptureAnalysisScheduler scheduler,
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisProjectionRefresher projectionRefresher,
        ICaptureAnalysisFeatureAvailability featureAvailability,
        IFileSystem fileSystem,
        ICaptureAnalysisCleanupCoordinator? cleanup = null)
    {
        ArgumentNullException.ThrowIfNull(changeReader);
        ArgumentNullException.ThrowIfNull(captureAssets);
        ArgumentNullException.ThrowIfNull(controlStore);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(jobStore);
        ArgumentNullException.ThrowIfNull(projectionRefresher);
        ArgumentNullException.ThrowIfNull(featureAvailability);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _changeReader = changeReader;
        _captureAssets = captureAssets;
        _controlStore = controlStore;
        _policyService = policyService;
        _scheduler = scheduler;
        _jobStore = jobStore;
        _projectionRefresher = projectionRefresher;
        _featureAvailability = featureAvailability;
        _fileSystem = fileSystem;
        _cleanup = cleanup;
    }

    public async Task ReconcileStartupAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cleanup != null)
            {
                _ = await _cleanup.ReconcileAsync(cancellationToken).ConfigureAwait(false);
            }

            await ConsumePendingChangesCoreAsync(cancellationToken).ConfigureAwait(false);
            await TryCompleteStartupAuditAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ConsumePendingChangesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cleanup != null)
            {
                _ = await _cleanup.ReconcileAsync(cancellationToken).ConfigureAwait(false);
            }

            await ConsumePendingChangesCoreAsync(cancellationToken).ConfigureAwait(false);
            await TryCompleteStartupAuditAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task TryCompleteStartupAuditAsync(CancellationToken cancellationToken)
    {
        if (!_featureAvailability.IsCaptureAnalysisEnabled)
        {
            _startupAuditCompleted = false;
            return;
        }

        if (_startupAuditCompleted)
        {
            return;
        }

        CaptureAnalysisPolicySnapshot policy = await _policyService
            .GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.IsProcessingAuthorized)
        {
            return;
        }

        await ReconcileEnrolledCapturesAsync(cancellationToken).ConfigureAwait(false);
        _startupAuditCompleted = true;
    }

    public async Task<CaptureAnalysisBackfillRunResult> RunAsync(
        IProgress<CaptureAnalysisBackfillProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        int scheduledCaptureCount = 0;
        try
        {
            if (!_featureAvailability.IsCaptureAnalysisEnabled)
            {
                return CreateBackfillResult(
                    CaptureAnalysisBackfillRunStatus.FeatureDisabled,
                    await GetBackfillProgressAsync(0, cancellationToken).ConfigureAwait(false));
            }

            BackfillTransition transition = await StartBackfillAsync(cancellationToken)
                .ConfigureAwait(false);
            if (transition.Status != BackfillTransitionStatus.InProgress)
            {
                return CreateBackfillResult(
                    transition.Status switch
                    {
                        BackfillTransitionStatus.Completed =>
                            CaptureAnalysisBackfillRunStatus.Completed,
                        BackfillTransitionStatus.AlreadyCompleted =>
                            CaptureAnalysisBackfillRunStatus.AlreadyCompleted,
                        BackfillTransitionStatus.NotAuthorized =>
                            CaptureAnalysisBackfillRunStatus.NotAuthorized,
                        BackfillTransitionStatus.Conflict =>
                            CaptureAnalysisBackfillRunStatus.Conflict,
                        _ => CaptureAnalysisBackfillRunStatus.Unavailable,
                    },
                    transition.Progress);
            }

            CaptureAnalysisBackfillProgress current = transition.Progress;
            progress?.Report(current);
            while (current.Checkpoint < current.UpperSequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CaptureAssetChangeBatch batch = await _changeReader
                    .ReadAfterAsync(current.Checkpoint, cancellationToken).ConfigureAwait(false);
                CaptureAssetChange[] boundedChanges = batch.Changes
                    .Where(change => change.Sequence <= current.UpperSequence)
                    .ToArray();
                if (boundedChanges.Length == 0)
                {
                    return CreateBackfillResult(
                        CaptureAnalysisBackfillRunStatus.Unavailable,
                        current);
                }

                foreach (CaptureAssetChange change in boundedChanges)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ChangeProcessingResult processed = await ProcessBackfillChangeAsync(
                        change,
                        cancellationToken).ConfigureAwait(false);
                    if (processed.Status != ChangeProcessingStatus.Processed)
                    {
                        return CreateBackfillResult(
                            processed.Status switch
                            {
                                ChangeProcessingStatus.NotAuthorized =>
                                    CaptureAnalysisBackfillRunStatus.NotAuthorized,
                                ChangeProcessingStatus.Conflict =>
                                    CaptureAnalysisBackfillRunStatus.Conflict,
                                _ => CaptureAnalysisBackfillRunStatus.Unavailable,
                            },
                            current);
                    }

                    if (processed.ScheduledCapture)
                    {
                        scheduledCaptureCount++;
                    }

                    BackfillTransition advanced = await AdvanceBackfillAsync(
                        change.Sequence,
                        scheduledCaptureCount,
                        cancellationToken).ConfigureAwait(false);
                    if (advanced.Status is not (
                        BackfillTransitionStatus.InProgress or BackfillTransitionStatus.Completed))
                    {
                        return CreateBackfillResult(
                            advanced.Status == BackfillTransitionStatus.NotAuthorized
                                ? CaptureAnalysisBackfillRunStatus.NotAuthorized
                                : advanced.Status == BackfillTransitionStatus.Conflict
                                    ? CaptureAnalysisBackfillRunStatus.Conflict
                                    : CaptureAnalysisBackfillRunStatus.Unavailable,
                            advanced.Progress);
                    }

                    current = advanced.Progress;
                    progress?.Report(current);
                }
            }

            return CreateBackfillResult(CaptureAnalysisBackfillRunStatus.Completed, current);
        }
        catch (OperationCanceledException)
        {
            CaptureAnalysisBackfillProgress current = await GetBackfillProgressAsync(
                scheduledCaptureCount,
                CancellationToken.None).ConfigureAwait(false);
            return CreateBackfillResult(CaptureAnalysisBackfillRunStatus.Cancelled, current);
        }
        catch
        {
            CaptureAnalysisBackfillProgress current = await GetBackfillProgressAsync(
                scheduledCaptureCount,
                CancellationToken.None).ConfigureAwait(false);
            return CreateBackfillResult(CaptureAnalysisBackfillRunStatus.Unavailable, current);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ConsumePendingChangesCoreAsync(CancellationToken cancellationToken)
    {
        CaptureAnalysisPolicySnapshot policy = await _policyService
            .GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        CaptureAnalysisControlSnapshot? control = policy.ControlSnapshot;
        if (control == null)
        {
            return;
        }

        bool featureEnabled = _featureAvailability.IsCaptureAnalysisEnabled;
        bool canSchedule = featureEnabled && policy.IsProcessingAuthorized;
        bool canAdvanceWithoutAnalysis = !featureEnabled ||
            policy.Status == CaptureAnalysisPolicySnapshotStatus.Available;
        if (!canSchedule && !canAdvanceWithoutAnalysis)
        {
            return;
        }

        long checkpoint = control.State.CaptureChangeCheckpoint;
        while (true)
        {
            CaptureAssetChangeBatch batch = await _changeReader
                .ReadAfterAsync(checkpoint, cancellationToken).ConfigureAwait(false);
            if (batch.Changes.Count == 0)
            {
                return;
            }

            foreach (CaptureAssetChange change in batch.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool processed;
                if (!canSchedule)
                {
                    processed = await CommitContentFreeChangeAsync(
                        change,
                        policy.SettingsConsentState,
                        preserveFutureEnrollment: !featureEnabled,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ChangeProcessingResult result = await ProcessLiveChangeAsync(
                        change,
                        cancellationToken).ConfigureAwait(false);
                    processed = result.Status == ChangeProcessingStatus.Processed &&
                        await AdvanceCaptureChangeCheckpointAsync(
                            change.Sequence,
                            cancellationToken).ConfigureAwait(false);
                }

                if (!processed)
                {
                    throw new InvalidOperationException(
                        "Capture Analysis intake could not durably process the next Capture change.");
                }

                checkpoint = change.Sequence;
            }

            if (!batch.HasMore)
            {
                return;
            }
        }
    }

    private async Task<ChangeProcessingResult> ProcessLiveChangeAsync(
        CaptureAssetChange change,
        CancellationToken cancellationToken)
    {
        CaptureAsset? asset = _captureAssets.Get(change.CaptureId);
        return change.ChangeType switch
        {
            CaptureAssetChangeType.Finalized => await ProcessFinalizationAsync(
                change,
                asset,
                CaptureAnalysisAdmissionKind.FutureCapture,
                cancellationToken).ConfigureAwait(false),
            CaptureAssetChangeType.SourceChanged => await ProcessSourceChangeAsync(
                change,
                asset,
                cancellationToken).ConfigureAwait(false),
            CaptureAssetChangeType.PreferredLocationChanged =>
                await ProcessPreferredLocationChangeAsync(change, cancellationToken)
                    .ConfigureAwait(false),
            CaptureAssetChangeType.Deleted => await ProcessDeletionAsync(
                change,
                cancellationToken).ConfigureAwait(false),
            _ => ChangeProcessingResult.Processed,
        };
    }

    private async Task<ChangeProcessingResult> ProcessBackfillChangeAsync(
        CaptureAssetChange change,
        CancellationToken cancellationToken)
    {
        if (change.ChangeType != CaptureAssetChangeType.Finalized)
        {
            return ChangeProcessingResult.Processed;
        }

        CaptureAsset? asset = _captureAssets.Get(change.CaptureId);
        return await ProcessFinalizationAsync(
            change,
            asset,
            CaptureAnalysisAdmissionKind.ExistingCaptureBackfill,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChangeProcessingResult> ProcessFinalizationAsync(
        CaptureAssetChange finalization,
        CaptureAsset? asset,
        CaptureAnalysisAdmissionKind admissionKind,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaptureMemoryRecipe(asset, out CaptureAnalysisRecipe recipe))
        {
            return ChangeProcessingResult.Processed;
        }

        return await ScheduleCaptureAsync(
            asset!,
            finalization,
            recipe,
            admissionKind,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChangeProcessingResult> ProcessSourceChangeAsync(
        CaptureAssetChange change,
        CaptureAsset? asset,
        CancellationToken cancellationToken)
    {
        if (!TryGetCaptureMemoryRecipe(asset, out CaptureAnalysisRecipe recipe))
        {
            return ChangeProcessingResult.Processed;
        }

        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        CaptureAnalysisEnrollment? enrollment = control.State.Enrollments.FirstOrDefault(
            candidate => candidate.CaptureId == change.CaptureId);
        if (enrollment?.State != CaptureAnalysisEnrollmentState.Enrolled)
        {
            return ChangeProcessingResult.Processed;
        }

        CaptureAssetChange? finalization = FindFinalization(change.CaptureId);
        _ = await EnsureCurrentRecipeAsync(change.CaptureId, recipe, cancellationToken)
            .ConfigureAwait(false);
        return finalization.HasValue
            ? await ScheduleCaptureAsync(
                asset!,
                finalization.Value,
                recipe,
                CaptureAnalysisAdmissionKind.FutureCapture,
                cancellationToken).ConfigureAwait(false)
            : ChangeProcessingResult.Unavailable;
    }

    private async Task<ChangeProcessingResult> ProcessDeletionAsync(
        CaptureAssetChange change,
        CancellationToken cancellationToken)
    {
        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        CaptureAnalysisEnrollment? enrollment = control.State.Enrollments.FirstOrDefault(
            candidate => candidate.CaptureId == change.CaptureId);
        if (enrollment == null || enrollment.State is
            CaptureAnalysisEnrollmentState.Excluded or CaptureAnalysisEnrollmentState.Forgotten)
        {
            return ChangeProcessingResult.Processed;
        }

        return await TombstoneMissingSourceAsync(
            change.CaptureId,
            enrollment.AssetFinalizationSequence,
            CaptureAnalysisExclusionReason.SourceDeleted,
            cancellationToken).ConfigureAwait(false)
                ? ChangeProcessingResult.Processed
                : ChangeProcessingResult.Unavailable;
    }

    private async Task<ChangeProcessingResult> ProcessPreferredLocationChangeAsync(
        CaptureAssetChange change,
        CancellationToken cancellationToken)
    {
        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        if (control.State.Enrollments.Any(enrollment =>
            enrollment.CaptureId == change.CaptureId &&
            enrollment.State == CaptureAnalysisEnrollmentState.Enrolled))
        {
            await _projectionRefresher.RefreshAsync(change.CaptureId, cancellationToken)
                .ConfigureAwait(false);
        }

        return ChangeProcessingResult.Processed;
    }

    private async Task<ChangeProcessingResult> ScheduleCaptureAsync(
        CaptureAsset asset,
        CaptureAssetChange finalization,
        CaptureAnalysisRecipe recipe,
        CaptureAnalysisAdmissionKind admissionKind,
        CancellationToken cancellationToken)
    {
        var request = new CaptureAnalysisScheduleRequest(
            new CaptureAnalysisAdmissionRequest(
                finalization,
                CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                admissionKind),
            recipe,
            ProcessingBoundary.OnDevice);
        CaptureAnalysisScheduleResult result = await _scheduler
            .ScheduleAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Status is CaptureAnalysisScheduleStatus.Scheduled or
            CaptureAnalysisScheduleStatus.AlreadyScheduled)
        {
            return new(ChangeProcessingStatus.Processed, ScheduledCapture: true);
        }

        if (result.Status == CaptureAnalysisScheduleStatus.SourceUnavailable)
        {
            bool isMissing;
            try
            {
                isMissing = !_fileSystem.FileExists(asset.RetainedSourcePath);
            }
            catch
            {
                return ChangeProcessingResult.Unavailable;
            }

            if (isMissing && await TombstoneMissingSourceAsync(
                asset.Id,
                finalization.Sequence,
                CaptureAnalysisExclusionReason.MissingSource,
                cancellationToken).ConfigureAwait(false))
            {
                return ChangeProcessingResult.Processed;
            }

            return ChangeProcessingResult.Unavailable;
        }

        if (result.Status == CaptureAnalysisScheduleStatus.Denied)
        {
            return result.DenialReason switch
            {
                CaptureAnalysisPolicyDenialReason.CaptureBeforeFutureWatermark or
                CaptureAnalysisPolicyDenialReason.BackfillNotAuthorized =>
                    admissionKind == CaptureAnalysisAdmissionKind.ExistingCaptureBackfill
                        ? ChangeProcessingResult.NotAuthorized
                        : ChangeProcessingResult.Processed,
                CaptureAnalysisPolicyDenialReason.CaptureExcluded or
                CaptureAnalysisPolicyDenialReason.PrivateCapture or
                CaptureAnalysisPolicyDenialReason.CaptureForgotten =>
                    ChangeProcessingResult.Processed,
                _ => ChangeProcessingResult.NotAuthorized,
            };
        }

        return result.Status == CaptureAnalysisScheduleStatus.Conflict
            ? ChangeProcessingResult.Conflict
            : ChangeProcessingResult.Unavailable;
    }

    private async Task ReconcileEnrolledCapturesAsync(CancellationToken cancellationToken)
    {
        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        foreach (CaptureAnalysisEnrollment enrollment in control.State.Enrollments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (enrollment.State == CaptureAnalysisEnrollmentState.Excluded &&
                enrollment.ExclusionReason is CaptureAnalysisExclusionReason.MissingSource or
                    CaptureAnalysisExclusionReason.SourceDeleted)
            {
                if (!await CompleteMissingSourceCleanupAsync(
                    enrollment,
                    cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "Capture Analysis could not complete missing-source cleanup.");
                }

                continue;
            }

            if (enrollment.State != CaptureAnalysisEnrollmentState.Enrolled)
            {
                continue;
            }

            CaptureAsset? asset = _captureAssets.Get(enrollment.CaptureId);
            if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active })
            {
                if (!await TombstoneMissingSourceAsync(
                    enrollment.CaptureId,
                    enrollment.AssetFinalizationSequence,
                    CaptureAnalysisExclusionReason.SourceDeleted,
                    cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "Capture Analysis could not tombstone a deleted source.");
                }

                continue;
            }

            if (!TryGetCaptureMemoryRecipe(asset, out CaptureAnalysisRecipe recipe))
            {
                continue;
            }

            CaptureAnalysisEnrollment? currentEnrollment = await EnsureCurrentRecipeAsync(
                enrollment.CaptureId,
                recipe,
                cancellationToken).ConfigureAwait(false);
            if (currentEnrollment?.State != CaptureAnalysisEnrollmentState.Enrolled)
            {
                continue;
            }

            bool sourceExists;
            try
            {
                sourceExists = _fileSystem.FileExists(asset.RetainedSourcePath);
            }
            catch
            {
                continue;
            }

            if (!sourceExists)
            {
                if (!await TombstoneMissingSourceAsync(
                    asset.Id,
                    currentEnrollment.AssetFinalizationSequence,
                    CaptureAnalysisExclusionReason.MissingSource,
                    cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "Capture Analysis could not tombstone a missing source.");
                }

                continue;
            }

            CaptureAssetChange? finalization = FindFinalization(asset.Id);
            if (!finalization.HasValue)
            {
                continue;
            }

            ChangeProcessingResult scheduled = await ScheduleCaptureAsync(
                asset,
                finalization.Value,
                recipe,
                CaptureAnalysisAdmissionKind.FutureCapture,
                cancellationToken).ConfigureAwait(false);
            if (scheduled.Status != ChangeProcessingStatus.Processed)
            {
                throw new InvalidOperationException(
                    "Capture Analysis could not reconcile an enrolled source.");
            }
        }
    }

    private async Task<bool> CommitContentFreeChangeAsync(
        CaptureAssetChange change,
        CaptureAnalysisConsentState settingsConsent,
        bool preserveFutureEnrollment,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            if (current.State.CaptureChangeCheckpoint >= change.Sequence)
            {
                return true;
            }

            List<CaptureAnalysisEnrollment> enrollments = [.. current.State.Enrollments];
            CaptureAnalysisPolicy policy = current.State.Policy;
            if (preserveFutureEnrollment &&
                settingsConsent == CaptureAnalysisConsentState.Granted &&
                policy.IsProcessingAuthorized &&
                policy.AuthorizationScope?.IsEquivalentTo(
                    CaptureAnalysisPolicyDefaults.CreateAuthorizationScope()) == true &&
                change.ChangeType == CaptureAssetChangeType.Finalized &&
                policy.IsFutureCaptureEligible(change.Sequence) &&
                !enrollments.Any(enrollment => enrollment.CaptureId == change.CaptureId) &&
                TryGetCaptureMemoryRecipe(
                    _captureAssets.Get(change.CaptureId),
                    out CaptureAnalysisRecipe recipe))
            {
                enrollments.Add(new CaptureAnalysisEnrollment(
                    change.CaptureId,
                    CaptureAnalysisEnrollmentState.Enrolled,
                    CaptureAnalysisExclusionReason.None,
                    enrollmentGeneration: 1,
                    tombstoneGeneration: 0,
                    change.Sequence,
                    recipe.Id,
                    recipe.Version));
            }

            var next = new CaptureAnalysisControlState(
                policy,
                enrollments,
                change.Sequence);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return true;
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return false;
            }
        }

        return false;
    }

    private async Task<bool> AdvanceCaptureChangeCheckpointAsync(
        long checkpoint,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            if (current.State.CaptureChangeCheckpoint >= checkpoint)
            {
                return true;
            }

            var next = new CaptureAnalysisControlState(
                current.State.Policy,
                current.State.Enrollments,
                checkpoint);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return true;
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return false;
            }
        }

        return false;
    }

    private async Task<CaptureAnalysisEnrollment?> EnsureCurrentRecipeAsync(
        CaptureId captureId,
        CaptureAnalysisRecipe recipe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            CaptureAnalysisEnrollment? enrollment = current.State.Enrollments.FirstOrDefault(
                candidate => candidate.CaptureId == captureId);
            if (enrollment?.State != CaptureAnalysisEnrollmentState.Enrolled)
            {
                return enrollment;
            }

            if (enrollment.RequestedRecipeId == recipe.Id &&
                enrollment.RequestedRecipeVersion == recipe.Version)
            {
                return enrollment;
            }

            var updated = new CaptureAnalysisEnrollment(
                enrollment.CaptureId,
                CaptureAnalysisEnrollmentState.Enrolled,
                CaptureAnalysisExclusionReason.None,
                checked(enrollment.EnrollmentGeneration + 1),
                enrollment.TombstoneGeneration,
                enrollment.AssetFinalizationSequence,
                recipe.Id,
                recipe.Version);
            var next = new CaptureAnalysisControlState(
                current.State.Policy,
                ReplaceEnrollment(current.State.Enrollments, updated),
                current.State.CaptureChangeCheckpoint);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return updated;
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                throw new InvalidOperationException(
                    "Capture Analysis could not update an enrolled capture recipe.");
            }
        }

        throw new InvalidOperationException(
            "Capture Analysis could not update an enrolled capture recipe after repeated conflicts.");
    }

    private static bool TryGetCaptureMemoryRecipe(
        CaptureAsset? asset,
        out CaptureAnalysisRecipe recipe)
    {
        if (asset is not
            {
                LifecycleState: CaptureAssetLifecycleState.Active,
                SourceOwnership: CaptureSourceOwnership.AppOwned,
            })
        {
            recipe = null!;
            return false;
        }

        CaptureMediaKind mediaKind = asset.MediaType switch
        {
            CaptureFileType.Image => CaptureMediaKind.Image,
            CaptureFileType.Audio => CaptureMediaKind.Audio,
            CaptureFileType.Video => CaptureMediaKind.Video,
            _ => CaptureMediaKind.Unknown,
        };
        bool found = CaptureAnalysisRecipeDefaults.TryCreateCaptureMemoryRecipe(
            mediaKind,
            out CaptureAnalysisRecipe? selected);
        recipe = selected!;
        return found;
    }

    private async Task<bool> TombstoneMissingSourceAsync(
        CaptureId captureId,
        long finalizationSequence,
        CaptureAnalysisExclusionReason reason,
        CancellationToken cancellationToken)
    {
        if (finalizationSequence <= 0)
        {
            return false;
        }

        CaptureAnalysisEnrollment? committed = null;
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            CaptureAnalysisEnrollment? existing = current.State.Enrollments.FirstOrDefault(
                candidate => candidate.CaptureId == captureId);
            if (existing?.State == CaptureAnalysisEnrollmentState.Forgotten)
            {
                return true;
            }

            if (existing is
                {
                    State: CaptureAnalysisEnrollmentState.Excluded,
                    ExclusionReason: CaptureAnalysisExclusionReason.MissingSource or
                        CaptureAnalysisExclusionReason.SourceDeleted,
                })
            {
                committed = existing;
                break;
            }

            if (existing?.State == CaptureAnalysisEnrollmentState.Excluded)
            {
                return true;
            }

            var tombstone = new CaptureAnalysisEnrollment(
                captureId,
                CaptureAnalysisEnrollmentState.Excluded,
                reason,
                checked((existing?.EnrollmentGeneration ?? 0) + 1),
                checked((existing?.TombstoneGeneration ?? 0) + 1),
                existing?.AssetFinalizationSequence ?? finalizationSequence,
                requestedRecipeId: null,
                requestedRecipeVersion: null);
            IReadOnlyList<CaptureAnalysisEnrollment> enrollments = existing == null
                ? [.. current.State.Enrollments, tombstone]
                : ReplaceEnrollment(current.State.Enrollments, tombstone);
            var next = new CaptureAnalysisControlState(
                current.State.Policy,
                enrollments,
                current.State.CaptureChangeCheckpoint);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                committed = tombstone;
                break;
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return false;
            }
        }

        return committed != null &&
            await CompleteMissingSourceCleanupAsync(committed, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<bool> CompleteMissingSourceCleanupAsync(
        CaptureAnalysisEnrollment tombstone,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await _jobStore.CancelCaptureAsync(
                tombstone.CaptureId,
                tombstone.TombstoneGeneration,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        CaptureAsset? asset = _captureAssets.Get(tombstone.CaptureId);
        if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active })
        {
            return true;
        }

        CaptureAssetCatalogWriteResult write = _captureAssets.TryUpdate(
            asset.MarkDeleted(),
            asset.LifecycleRevision,
            CaptureAssetChangeType.Deleted);
        return write.Succeeded;
    }

    private async Task<BackfillTransition> StartBackfillAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisPolicySnapshot policySnapshot = await _policyService
                .GetCurrentAsync(cancellationToken).ConfigureAwait(false);
            if (!policySnapshot.IsProcessingAuthorized || policySnapshot.ControlSnapshot == null)
            {
                return new(
                    BackfillTransitionStatus.NotAuthorized,
                    GetBackfillProgress(policySnapshot.ControlSnapshot?.State, 0));
            }

            CaptureAnalysisControlSnapshot current = policySnapshot.ControlSnapshot;
            if (current.State.BackfillState == CaptureAnalysisBackfillState.Completed)
            {
                return new(
                    BackfillTransitionStatus.AlreadyCompleted,
                    GetBackfillProgress(current.State, 0));
            }

            if (current.State.BackfillState == CaptureAnalysisBackfillState.InProgress)
            {
                return new(
                    BackfillTransitionStatus.InProgress,
                    GetBackfillProgress(current.State, 0));
            }

            if (current.State.BackfillState != CaptureAnalysisBackfillState.Authorized)
            {
                return new(
                    BackfillTransitionStatus.NotAuthorized,
                    GetBackfillProgress(current.State, 0));
            }

            CaptureAnalysisPolicy nextPolicy = current.State.Policy.StartExistingCaptureBackfill();
            var next = new CaptureAnalysisControlState(
                nextPolicy,
                current.State.Enrollments,
                current.State.CaptureChangeCheckpoint);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return new(
                    nextPolicy.BackfillState == CaptureAnalysisBackfillState.Completed
                        ? BackfillTransitionStatus.Completed
                        : BackfillTransitionStatus.InProgress,
                    GetBackfillProgress(next, 0));
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return new(
                    BackfillTransitionStatus.Unavailable,
                    GetBackfillProgress(current.State, 0));
            }
        }

        return new(
            BackfillTransitionStatus.Conflict,
            await GetBackfillProgressAsync(0, cancellationToken).ConfigureAwait(false));
    }

    private async Task<BackfillTransition> AdvanceBackfillAsync(
        long checkpoint,
        int scheduledCaptureCount,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumControlWriteAttempts; attempt++)
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            if (current.State.BackfillState == CaptureAnalysisBackfillState.Completed ||
                current.State.BackfillCheckpoint >= checkpoint)
            {
                return new(
                    current.State.BackfillState == CaptureAnalysisBackfillState.Completed
                        ? BackfillTransitionStatus.Completed
                        : BackfillTransitionStatus.InProgress,
                    GetBackfillProgress(current.State, scheduledCaptureCount));
            }

            if (current.State.BackfillState is not (
                CaptureAnalysisBackfillState.Authorized or CaptureAnalysisBackfillState.InProgress))
            {
                return new(
                    BackfillTransitionStatus.NotAuthorized,
                    GetBackfillProgress(current.State, scheduledCaptureCount));
            }

            CaptureAnalysisPolicy nextPolicy = current.State.Policy
                .AdvanceExistingCaptureBackfill(checkpoint);
            var next = new CaptureAnalysisControlState(
                nextPolicy,
                current.State.Enrollments,
                current.State.CaptureChangeCheckpoint);
            CaptureAnalysisControlWriteResult write = await _controlStore.TryWriteAsync(
                next,
                current.DocumentRevision,
                cancellationToken).ConfigureAwait(false);
            if (write.Status == CaptureAnalysisControlWriteStatus.Succeeded)
            {
                return new(
                    nextPolicy.BackfillState == CaptureAnalysisBackfillState.Completed
                        ? BackfillTransitionStatus.Completed
                        : BackfillTransitionStatus.InProgress,
                    GetBackfillProgress(next, scheduledCaptureCount));
            }

            if (write.Status != CaptureAnalysisControlWriteStatus.Conflict)
            {
                return new(
                    BackfillTransitionStatus.Unavailable,
                    GetBackfillProgress(current.State, scheduledCaptureCount));
            }
        }

        return new(
            BackfillTransitionStatus.Conflict,
            await GetBackfillProgressAsync(scheduledCaptureCount, cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task<CaptureAnalysisBackfillProgress> GetBackfillProgressAsync(
        int scheduledCaptureCount,
        CancellationToken cancellationToken)
    {
        try
        {
            CaptureAnalysisControlSnapshot current = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            return GetBackfillProgress(current.State, scheduledCaptureCount);
        }
        catch
        {
            return new(0, 0, scheduledCaptureCount);
        }
    }

    private static CaptureAnalysisBackfillProgress GetBackfillProgress(
        CaptureAnalysisControlState? state,
        int scheduledCaptureCount)
    {
        return state == null
            ? new(0, 0, scheduledCaptureCount)
            : new(
                state.BackfillCheckpoint,
                state.BackfillUpperSequence,
                scheduledCaptureCount);
    }

    private CaptureAssetChange? FindFinalization(CaptureId captureId)
    {
        CaptureAssetChange change = _captureAssets
            .GetChangesAfter(0)
            .FirstOrDefault(candidate =>
                candidate.CaptureId == captureId &&
                candidate.ChangeType == CaptureAssetChangeType.Finalized);
        return change.Sequence > 0 ? change : null;
    }

    private long GetFinalizationSequence(CaptureId captureId)
    {
        return FindFinalization(captureId)?.Sequence ?? 0;
    }

    private static IReadOnlyList<CaptureAnalysisEnrollment> ReplaceEnrollment(
        IReadOnlyList<CaptureAnalysisEnrollment> enrollments,
        CaptureAnalysisEnrollment replacement)
    {
        return enrollments
            .Select(enrollment => enrollment.CaptureId == replacement.CaptureId
                ? replacement
                : enrollment)
            .ToArray();
    }

    private static CaptureAnalysisBackfillRunResult CreateBackfillResult(
        CaptureAnalysisBackfillRunStatus status,
        CaptureAnalysisBackfillProgress progress)
    {
        return new(status, progress);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private enum ChangeProcessingStatus
    {
        Processed,
        NotAuthorized,
        Conflict,
        Unavailable,
    }

    private readonly record struct ChangeProcessingResult(
        ChangeProcessingStatus Status,
        bool ScheduledCapture = false)
    {
        public static ChangeProcessingResult Processed { get; } = new(
            ChangeProcessingStatus.Processed);

        public static ChangeProcessingResult NotAuthorized { get; } = new(
            ChangeProcessingStatus.NotAuthorized);

        public static ChangeProcessingResult Conflict { get; } = new(
            ChangeProcessingStatus.Conflict);

        public static ChangeProcessingResult Unavailable { get; } = new(
            ChangeProcessingStatus.Unavailable);
    }

    private enum BackfillTransitionStatus
    {
        InProgress,
        Completed,
        AlreadyCompleted,
        NotAuthorized,
        Conflict,
        Unavailable,
    }

    private readonly record struct BackfillTransition(
        BackfillTransitionStatus Status,
        CaptureAnalysisBackfillProgress Progress);
}
