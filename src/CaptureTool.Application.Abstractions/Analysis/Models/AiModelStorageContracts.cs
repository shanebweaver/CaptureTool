namespace CaptureTool.Application.Abstractions.Analysis.Models;

public sealed record AiModelStorageSnapshot
{
    public AiModelStorageSnapshot(long downloadedByteCount, bool measurementSucceeded = true)
    {
        if (downloadedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(downloadedByteCount));
        }

        DownloadedByteCount = downloadedByteCount;
        MeasurementSucceeded = measurementSucceeded;
    }

    public long DownloadedByteCount { get; }

    public bool MeasurementSucceeded { get; }
}

public sealed record AiModelStorageRemovalResult
{
    public AiModelStorageRemovalResult(
        int removedModelCount,
        long reclaimedByteCount,
        long remainingByteCount,
        int failedModelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(removedModelCount);
        ArgumentOutOfRangeException.ThrowIfNegative(reclaimedByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedModelCount);

        RemovedModelCount = removedModelCount;
        ReclaimedByteCount = reclaimedByteCount;
        RemainingByteCount = remainingByteCount;
        FailedModelCount = failedModelCount;
    }

    public int RemovedModelCount { get; }

    public long ReclaimedByteCount { get; }

    public long RemainingByteCount { get; }

    public int FailedModelCount { get; }

    public bool Succeeded => FailedModelCount == 0;
}

public interface IAiModelStorageService
{
    ValueTask<AiModelStorageSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    ValueTask<AiModelStorageRemovalResult> RemoveDownloadedModelsAsync(
        CancellationToken cancellationToken = default);
}
