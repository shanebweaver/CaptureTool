using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

public sealed record CaptureAssetCatalogWriteResult(
    bool Succeeded,
    bool Changed,
    CaptureAsset? Asset,
    long? ChangeSequence)
{
    public static CaptureAssetCatalogWriteResult Failed { get; } = new(false, false, null, null);

    public static CaptureAssetCatalogWriteResult Unchanged(CaptureAsset asset, long? changeSequence) =>
        new(true, false, asset, changeSequence);

    public static CaptureAssetCatalogWriteResult Committed(CaptureAsset asset, long changeSequence) =>
        new(true, true, asset, changeSequence);
}
