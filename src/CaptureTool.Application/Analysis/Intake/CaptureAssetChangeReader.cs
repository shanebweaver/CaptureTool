using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Intake;

internal sealed class CaptureAssetChangeReader : ICaptureAssetChangeReader
{
    private const int MaximumBatchSize = 128;
    private readonly ICaptureAssetCatalog _captureAssets;

    public CaptureAssetChangeReader(ICaptureAssetCatalog captureAssets)
    {
        ArgumentNullException.ThrowIfNull(captureAssets);
        _captureAssets = captureAssets;
    }

    public ValueTask<CaptureAssetChangeBatch> ReadAfterAsync(
        long checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();

        long latestSequence = _captureAssets.GetLatestChangeSequence();
        if (checkpoint > latestSequence)
        {
            throw new InvalidDataException(
                "The Capture Analysis checkpoint is ahead of the Capture Asset change feed.");
        }

        CaptureAssetChange[] changes = _captureAssets
            .GetChangesAfter(checkpoint)
            .Take(MaximumBatchSize)
            .ToArray();
        long nextCheckpoint = changes.Length == 0 ? checkpoint : changes[^1].Sequence;
        return ValueTask.FromResult(new CaptureAssetChangeBatch(
            checkpoint,
            nextCheckpoint,
            latestSequence,
            changes));
    }
}
