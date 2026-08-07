using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Tests.Analysis.Intake;

[TestClass]
public sealed class CaptureAssetChangeReaderTests
{
    [TestMethod]
    public async Task ReadAfter_ShouldReturnBoundedOrderedBatches()
    {
        CaptureId captureId = CaptureId.New();
        DateTimeOffset changedAtUtc = new(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);
        CaptureAssetChange[] changes = Enumerable.Range(1, 130)
            .Select(sequence => new CaptureAssetChange(
                sequence,
                captureId,
                lifecycleRevision: sequence,
                CaptureAssetChangeType.SourceChanged,
                changedAtUtc.AddSeconds(sequence)))
            .ToArray();
        var reader = new CaptureAssetChangeReader(new ChangeOnlyCatalog(changes));

        var first = await reader.ReadAfterAsync(0);
        var second = await reader.ReadAfterAsync(first.NextCheckpoint);

        Assert.HasCount(128, first.Changes);
        Assert.AreEqual(128, first.NextCheckpoint);
        Assert.IsTrue(first.HasMore);
        Assert.HasCount(2, second.Changes);
        Assert.AreEqual(130, second.NextCheckpoint);
        Assert.IsFalse(second.HasMore);
    }

    private sealed class ChangeOnlyCatalog(IReadOnlyList<CaptureAssetChange> changes) :
        ICaptureAssetCatalog
    {
        public IReadOnlyList<CaptureAsset> GetAssets() => [];
        public CaptureAsset? Get(CaptureId captureId) => null;
        public CaptureAsset? FindByPath(string filePath) => null;
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) =>
            changes.Where(change => change.Sequence > sequence).ToArray();
        public long GetLatestChangeSequence() => changes[^1].Sequence;
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryUpdate(CaptureAsset asset, long expectedLifecycleRevision, CaptureAssetChangeType changeType) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryForget(CaptureId captureId, long expectedLifecycleRevision) => throw new NotSupportedException();
    }
}
