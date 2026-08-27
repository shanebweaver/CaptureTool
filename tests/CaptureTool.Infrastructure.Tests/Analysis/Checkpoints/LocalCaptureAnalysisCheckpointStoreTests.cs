using CaptureTool.Application.Abstractions.Analysis.Checkpoints;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Checkpoints;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Tests.Analysis.Persistence;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis.Checkpoints;

[TestClass]
public sealed class LocalCaptureAnalysisCheckpointStoreTests
{
    [TestMethod]
    public async Task Contracts_ShouldRejectIncompleteKeysAndInvalidMaintenanceArguments()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            _ = new CaptureAnalysisCheckpointKey(
                default,
                AnalysisPersistenceTestData.SourceRevision,
                AnalysisCapabilities.VideoOcrTrackV1,
                AnalysisPersistenceTestData.Analyzer.Revision));

        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        Assert.ThrowsExactly<ArgumentException>(() => store.Open(default));
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await store.DeleteCaptureAsync(default));
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await store.PruneAsync(new DateTimeOffset(
                2026,
                8,
                11,
                12,
                0,
                0,
                TimeSpan.FromHours(1))));
        Assert.AreEqual(0, await store.PruneAsync(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));
    }

    [TestMethod]
    public async Task Checkpoint_ShouldRoundTripProtectedPayloadAndDeleteByCapture()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            protector,
            new AtomicFileWriter(),
            new TestLogService());
        CaptureId captureId = CaptureId.New();
        ICaptureAnalysisCheckpointStore contract = store;
        var key = new CaptureAnalysisCheckpointKey(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            AnalysisCapabilities.VideoOcrTrackV1,
            AnalysisPersistenceTestData.Analyzer.Revision);
        byte[] payload = Encoding.UTF8.GetBytes("PRIVATE-CHECKPOINT-CONTENT");

        await contract.Open(key).WriteAsync(payload);
        string checkpointPath = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).Single();

        Assert.DoesNotContain(
            "PRIVATE-CHECKPOINT-CONTENT",
            Encoding.UTF8.GetString(File.ReadAllBytes(checkpointPath)));
        ReadOnlyMemory<byte>? restored = await contract.Open(key).ReadAsync();
        Assert.IsTrue(restored.HasValue);
        CollectionAssert.AreEqual(payload, restored.Value.ToArray());

        await contract.DeleteCaptureAsync(captureId);

        Assert.IsNull(await contract.Open(key).ReadAsync());
        Assert.IsFalse(File.Exists(checkpointPath));
    }

    [TestMethod]
    public async Task Checkpoint_ShouldPreservePriorPayloadWhenAtomicReplacementIsInterrupted()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var writer = new InterruptingAtomicFileWriter();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            writer,
            new TestLogService());
        CaptureAnalysisCheckpointKey key = CreateKey(CaptureId.New());
        byte[] original = [1, 2, 3];
        await store.Open(key).WriteAsync(original);
        writer.InterruptNextWrite = true;

        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await store.Open(key).WriteAsync(new byte[] { 9, 8, 7 }));

        ReadOnlyMemory<byte>? restored = await store.Open(key).ReadAsync();
        Assert.IsTrue(restored.HasValue);
        CollectionAssert.AreEqual(original, restored.Value.ToArray());
    }

    [TestMethod]
    public async Task UnreadableCheckpoint_ShouldBeDiscardedWithoutExposingPayload()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        var key = new CaptureAnalysisCheckpointKey(
            CaptureId.New(),
            AnalysisPersistenceTestData.SourceRevision,
            AnalysisCapabilities.VideoOcrTrackV1,
            AnalysisPersistenceTestData.Analyzer.Revision);
        await store.Open(key).WriteAsync(new byte[] { 1, 2, 3 });
        string checkpointPath = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).Single();
        File.WriteAllBytes(checkpointPath, [9, 8, 7]);

        ReadOnlyMemory<byte>? restored = await store.Open(key).ReadAsync();

        Assert.IsNull(restored);
        Assert.IsFalse(File.Exists(checkpointPath));
    }

    [TestMethod]
    public async Task Checkpoint_ShouldBindProtectedPayloadToExactCapabilityKey()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        CaptureId captureId = CaptureId.New();
        CaptureAnalysisCheckpointKey extracted = CreateKey(captureId);
        var inferredDefinition = new CapabilityDefinition(
            AnalysisCapabilities.VideoOcrTrackV1.Id,
            AnalysisCapabilities.VideoOcrTrackV1.SchemaVersion,
            CapabilityResultClassification.Inference);
        var inferred = new CaptureAnalysisCheckpointKey(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            inferredDefinition,
            AnalysisPersistenceTestData.Analyzer.Revision);
        await store.Open(extracted).WriteAsync(new byte[] { 1, 2, 3 });
        string extractedPath = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).Single();
        await store.Open(inferred).WriteAsync(new byte[] { 4, 5, 6 });
        string inferredPath = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).Single(path => path != extractedPath);
        File.WriteAllBytes(inferredPath, File.ReadAllBytes(extractedPath));

        Assert.IsNull(await store.Open(inferred).ReadAsync());
        Assert.IsTrue((await store.Open(extracted).ReadAsync()).HasValue);
        Assert.IsFalse(File.Exists(inferredPath));
    }

    [TestMethod]
    public async Task Prune_ShouldBoundAbandonedCheckpointRetention()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        var key = new CaptureAnalysisCheckpointKey(
            CaptureId.New(),
            AnalysisPersistenceTestData.SourceRevision,
            AnalysisCapabilities.VideoOcrTrackV1,
            AnalysisPersistenceTestData.Analyzer.Revision);
        CaptureAnalysisCheckpointKey fresh = CreateKey(CaptureId.New());
        await store.Open(key).WriteAsync(new byte[] { 1 });
        string oldPath = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).Single();
        await store.Open(fresh).WriteAsync(new byte[] { 2 });
        DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        string[] checkpointPaths = Directory.EnumerateFiles(
            root,
            $"*{LocalCaptureAnalysisCheckpointStore.CheckpointExtension}",
            SearchOption.AllDirectories).ToArray();
        Assert.HasCount(2, checkpointPaths);
        File.SetLastWriteTimeUtc(oldPath, now.AddDays(-8).UtcDateTime);

        int removed = await store.PruneAsync(now.AddDays(-7));

        Assert.AreEqual(1, removed);
        Assert.IsFalse(File.Exists(oldPath));
        Assert.IsTrue(checkpointPaths.Where(path => path != oldPath).All(File.Exists));
    }

    [TestMethod]
    public async Task Checkpoint_ShouldRejectPayloadAboveBoundedLimit()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        byte[] oversized = GC.AllocateUninitializedArray<byte>(
            LocalCaptureAnalysisCheckpointStore.MaximumPayloadBytes + 1);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(async () =>
            await store.Open(CreateKey(CaptureId.New())).WriteAsync(oversized));

        Assert.IsFalse(Directory.Exists(Path.Combine(
            root,
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            LocalCaptureAnalysisCheckpointStore.CheckpointsVersionDirectoryName)));
    }

    [TestMethod]
    public async Task ClearAndCheckpointClear_ShouldDeleteOnlyManagedDisposableState()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureAnalysisCheckpointStore(
            new TestLocalCachePathProvider(root),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        CaptureAnalysisCheckpointKey first = CreateKey(CaptureId.New());
        CaptureAnalysisCheckpointKey second = CreateKey(CaptureId.New());
        await store.Open(first).WriteAsync(new byte[] { 1 });
        await store.Open(second).WriteAsync(new byte[] { 2 });

        await store.Open(first).ClearAsync();

        Assert.IsNull(await store.Open(first).ReadAsync());
        Assert.IsTrue((await store.Open(second).ReadAsync()).HasValue);

        await store.ClearAsync();

        Assert.IsNull(await store.Open(second).ReadAsync());
    }

    private static CaptureAnalysisCheckpointKey CreateKey(CaptureId captureId)
    {
        return new(
            captureId,
            AnalysisPersistenceTestData.SourceRevision,
            AnalysisCapabilities.VideoOcrTrackV1,
            AnalysisPersistenceTestData.Analyzer.Revision);
    }
}
