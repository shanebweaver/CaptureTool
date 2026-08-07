using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Maintenance;

internal sealed class CaptureAnalysisCleanupCoordinator : ICaptureAnalysisCleanupCoordinator
{
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAnalysisJobStore _jobStore;
    private readonly ICaptureAnalysisStore _metadataStore;
    private readonly ICaptureAnalysisMutationCoordinator _mutationCoordinator;
    private readonly ICaptureAnalysisProjectionMaintenance _projectionMaintenance;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly IRecentCaptureCatalog _recentCaptures;
    private readonly IFileSystem _fileSystem;

    public CaptureAnalysisCleanupCoordinator(
        ICaptureAnalysisControlStore controlStore,
        ICaptureAnalysisJobStore jobStore,
        ICaptureAnalysisStore metadataStore,
        ICaptureAnalysisMutationCoordinator mutationCoordinator,
        ICaptureAnalysisProjectionMaintenance projectionMaintenance,
        ICaptureAssetCatalog captureAssets,
        IRecentCaptureCatalog recentCaptures,
        IFileSystem fileSystem)
    {
        _controlStore = controlStore;
        _jobStore = jobStore;
        _metadataStore = metadataStore;
        _mutationCoordinator = mutationCoordinator;
        _projectionMaintenance = projectionMaintenance;
        _captureAssets = captureAssets;
        _recentCaptures = recentCaptures;
        _fileSystem = fileSystem;
    }

    public async ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        CaptureAnalysisControlSnapshot control;
        try
        {
            control = await _controlStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (control.State.ControlGeneration > 0)
            {
                _ = await _jobStore.CancelBeforeControlGenerationAsync(
                    control.State.ControlGeneration,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }

        bool completed = true;
        foreach (CaptureAnalysisEnrollment tombstone in control.State.Enrollments.Where(
            enrollment => enrollment.State is CaptureAnalysisEnrollmentState.Excluded or
                CaptureAnalysisEnrollmentState.Forgotten))
        {
            completed &= await ReconcileCaptureCoreAsync(control, tombstone, cancellationToken)
                .ConfigureAwait(false);
        }

        return completed;
    }

    public async ValueTask<bool> ReconcileCaptureAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Cleanup requires a capture ID.", nameof(captureId));
        }

        try
        {
            CaptureAnalysisControlSnapshot control = await _controlStore
                .GetAsync(cancellationToken).ConfigureAwait(false);
            CaptureAnalysisEnrollment? tombstone = control.State.Enrollments.FirstOrDefault(
                enrollment => enrollment.CaptureId == captureId &&
                    enrollment.State is CaptureAnalysisEnrollmentState.Excluded or
                        CaptureAnalysisEnrollmentState.Forgotten);
            return tombstone == null ||
                await ReconcileCaptureCoreAsync(control, tombstone, cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async ValueTask<bool> ReconcileCaptureCoreAsync(
        CaptureAnalysisControlSnapshot control,
        CaptureAnalysisEnrollment tombstone,
        CancellationToken cancellationToken)
    {
        try
        {
            if (tombstone is
                {
                    State: CaptureAnalysisEnrollmentState.Forgotten,
                    ExclusionReason: CaptureAnalysisExclusionReason.DeleteRequested,
                } && !DeleteAppOwnedRetainedSource(tombstone.CaptureId))
            {
                return false;
            }

            _ = await _jobStore.CancelCaptureAsync(
                tombstone.CaptureId,
                tombstone.TombstoneGeneration,
                cancellationToken).ConfigureAwait(false);

            CaptureAnalysisStoreSnapshot? metadata = await _metadataStore
                .GetAsync(tombstone.CaptureId, cancellationToken).ConfigureAwait(false);
            if (metadata != null)
            {
                CaptureAnalysisStoreWriteResult deleted = await _mutationCoordinator.TryDeleteAsync(
                    new CaptureAnalysisDeletionToken(
                        tombstone.CaptureId,
                        control.State.ControlGeneration,
                        tombstone.TombstoneGeneration),
                    metadata.DocumentRevision,
                    cancellationToken).ConfigureAwait(false);
                if (deleted.Status is not (
                    CaptureAnalysisStoreWriteStatus.Succeeded or
                    CaptureAnalysisStoreWriteStatus.NotFound))
                {
                    return false;
                }
            }

            await _projectionMaintenance.RemoveAsync(tombstone.CaptureId, cancellationToken)
                .ConfigureAwait(false);

            if (tombstone.State != CaptureAnalysisEnrollmentState.Forgotten)
            {
                return true;
            }

            RemoveRecentProjection(tombstone.CaptureId);
            CaptureAsset? asset = _captureAssets.Get(tombstone.CaptureId);
            if (asset == null)
            {
                return true;
            }

            CaptureAssetCatalogWriteResult forgotten = _captureAssets.TryForget(
                asset.Id,
                asset.LifecycleRevision);
            return forgotten.Succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private bool DeleteAppOwnedRetainedSource(CaptureId captureId)
    {
        CaptureAsset? asset = _captureAssets.Get(captureId);
        if (asset == null)
        {
            return true;
        }

        if (asset.SourceOwnership != CaptureSourceOwnership.AppOwned)
        {
            return false;
        }

        // RetainedSourcePath is the only deletion target. PreferredOpenPath can point at a user
        // export and is deliberately never consulted by this command.
        if (_fileSystem.FileExists(asset.RetainedSourcePath))
        {
            _fileSystem.DeleteFile(asset.RetainedSourcePath);
        }

        return !_fileSystem.FileExists(asset.RetainedSourcePath);
    }

    private void RemoveRecentProjection(CaptureId captureId)
    {
        string[] paths = _recentCaptures.GetEntries()
            .Where(entry => entry.CaptureId == captureId)
            .Select(entry => entry.FilePath)
            .ToArray();
        if (paths.Length > 0)
        {
            _ = _recentCaptures.RemoveRange(paths);
        }
    }
}
