using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Assets;

internal sealed class CaptureAssetLifecycleService : ICaptureAssetLifecycleService
{
    private readonly ICaptureAssetCatalog _captureAssetCatalog;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly ICaptureAssetChangeSignal _changeSignal;
    private readonly IClock _clock;
    private readonly ILogService _logService;

    public CaptureAssetLifecycleService(
        ICaptureAssetCatalog captureAssetCatalog,
        IRecentCaptureCatalog recentCaptureCatalog,
        ICaptureAssetChangeSignal changeSignal,
        IClock clock,
        ILogService logService)
    {
        _captureAssetCatalog = captureAssetCatalog;
        _recentCaptureCatalog = recentCaptureCatalog;
        _changeSignal = changeSignal;
        _clock = clock;
        _logService = logService;
    }

    public CaptureId? TryFinalize(string retainedSourcePath, CaptureFileType mediaType)
    {
        CaptureAssetCatalogWriteResult result = TryCommitFinalizedAsset(retainedSourcePath, mediaType);
        if (!result.Succeeded || result.Asset is null || result.ChangeSequence is not long changeSequence)
        {
            TryRecordCaptureWithoutIdentity(retainedSourcePath, mediaType);
            return null;
        }

        string recentPath = result.Asset.PreferredOpenPath ?? retainedSourcePath;
        TryProjectCapture(
            recentPath,
            result.Asset.MediaType,
            result.Asset.Id,
            changeSequence,
            _clock.UtcNow);
        TrySignal();
        return result.Asset.Id;
    }

    public void TrySetPreferredOpenPath(
        CaptureId? captureId,
        string retainedSourcePath,
        string preferredOpenPath)
    {
        if (captureId is not CaptureId id)
        {
            return;
        }

        CaptureAssetCatalogWriteResult result = TryCommitPreferredOpenPath(id, preferredOpenPath);
        if (!result.Succeeded || result.Asset is null || result.ChangeSequence is not long changeSequence)
        {
            TryRepairRecentProjection(id, retainedSourcePath, preferredOpenPath);
            return;
        }

        TryProjectCapture(
            preferredOpenPath,
            result.Asset.MediaType,
            result.Asset.Id,
            changeSequence,
            _clock.UtcNow);
        TrySignal();
    }

    private CaptureAssetCatalogWriteResult TryCommitFinalizedAsset(
        string retainedSourcePath,
        CaptureFileType mediaType)
    {
        try
        {
            CaptureAsset asset = CaptureAsset.Create(
                mediaType,
                retainedSourcePath,
                CaptureSourceOwnership.AppOwned,
                GetUtcNow());
            return _captureAssetCatalog.TryAdd(asset);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to persist finalized capture identity.");
            return CaptureAssetCatalogWriteResult.Failed;
        }
    }

    private CaptureAssetCatalogWriteResult TryCommitPreferredOpenPath(
        CaptureId captureId,
        string preferredOpenPath)
    {
        try
        {
            CaptureAsset? existing = _captureAssetCatalog.Get(captureId);
            if (existing is null)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            CaptureAsset updated = existing.ChangePreferredOpenPath(preferredOpenPath);
            return _captureAssetCatalog.TryUpdate(
                updated,
                existing.LifecycleRevision,
                CaptureAssetChangeType.PreferredLocationChanged);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to persist a capture preferred location.");
            return CaptureAssetCatalogWriteResult.Failed;
        }
    }

    private void TryProjectCapture(
        string filePath,
        CaptureFileType mediaType,
        CaptureId captureId,
        long changeSequence,
        DateTime activityUtc)
    {
        try
        {
            if (!_recentCaptureCatalog.TryProjectCaptured(
                filePath,
                mediaType,
                captureId,
                changeSequence,
                activityUtc))
            {
                _logService.LogWarning("The recent capture projection could not be persisted.");
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to update recent history for a capture asset.");
        }
    }

    private void TryRecordCaptureWithoutIdentity(string filePath, CaptureFileType mediaType)
    {
        try
        {
            _recentCaptureCatalog.RecordCaptured(filePath, mediaType);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to record a capture without durable identity.");
        }
    }

    private void TryRepairRecentProjection(
        CaptureId captureId,
        string retainedSourcePath,
        string preferredOpenPath)
    {
        try
        {
            CaptureFileType mediaType = CaptureFileTypeDetector.DetectFileType(preferredOpenPath);
            if (!_recentCaptureCatalog.TryRepairCapturedProjection(
                retainedSourcePath,
                preferredOpenPath,
                mediaType,
                captureId,
                _clock.UtcNow))
            {
                _logService.LogWarning("The recent capture location repair could not be persisted.");
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to repair a recent capture location.");
        }
    }

    private void TrySignal()
    {
        try
        {
            _ = _changeSignal.TrySignal();
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to signal capture asset changes.");
        }
    }

    private DateTimeOffset GetUtcNow()
    {
        DateTime utcNow = _clock.UtcNow;
        return new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }
}
