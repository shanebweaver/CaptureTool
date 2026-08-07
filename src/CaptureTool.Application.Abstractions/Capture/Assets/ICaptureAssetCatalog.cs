using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public interface ICaptureAssetCatalog
{
    IReadOnlyList<CaptureAsset> GetAssets();

    CaptureAsset? Get(CaptureId captureId);

    CaptureAsset? FindByPath(string filePath);

    IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence);

    long GetLatestChangeSequence();

    CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset);

    IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets);

    CaptureAssetCatalogWriteResult TryUpdate(
        CaptureAsset asset,
        long expectedLifecycleRevision,
        CaptureAssetChangeType changeType);

    CaptureAssetCatalogWriteResult TryForget(
        CaptureId captureId,
        long expectedLifecycleRevision);
}
