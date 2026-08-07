using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis.Persistence;

[TestClass]
public sealed class LocalCaptureAnalysisControlStoreTests
{
    [TestMethod]
    public async Task WriteAndReload_ShouldPreserveProtectedPolicyAndEnrollmentLedger()
    {
        string dataFolder = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        LocalCaptureAnalysisControlStore store = CreateStore(dataFolder, protector);
        CaptureAnalysisControlSnapshot initial = await store.GetAsync();
        CaptureId captureId = CaptureId.New();
        CaptureAnalysisControlState defaultState =
            AnalysisPersistenceTestData.CreateControlState(captureId);
        var persistedPolicy = new CaptureAnalysisPolicy(
            CaptureAnalysisConsentState.Granted,
            policyRevision: 7,
            controlGeneration: 9,
            defaultState.AuthorizationScope,
            isFutureCaptureAdmissionEnabled: true,
            futureCaptureSequenceWatermark: 40,
            CaptureAnalysisBackfillState.InProgress,
            backfillUpperSequence: 39,
            backfillCheckpoint: 12);
        var state = new CaptureAnalysisControlState(persistedPolicy, defaultState.Enrollments);

        CaptureAnalysisControlWriteResult result = await store.TryWriteAsync(
            state,
            initial.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisControlWriteStatus.Succeeded, result.Status);
        Assert.AreEqual(2, result.Snapshot!.DocumentRevision);
        string filePath = store.GetControlFilePath();
        byte[] protectedBytes = File.ReadAllBytes(filePath);
        string protectedText = Encoding.UTF8.GetString(protectedBytes);
        Assert.DoesNotContain(captureId.ToString(), protectedText);
        Assert.DoesNotContain("capture-memory-search", protectedText);

        using LocalCaptureAnalysisControlStore reloaded = CreateStore(dataFolder, protector);
        CaptureAnalysisControlSnapshot persisted = await reloaded.GetAsync();
        Assert.AreEqual(2, persisted.DocumentRevision);
        Assert.AreEqual(CaptureAnalysisConsentState.Granted, persisted.State.ConsentState);
        Assert.IsTrue(persisted.State.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(7, persisted.State.PolicyRevision);
        Assert.AreEqual(9, persisted.State.ControlGeneration);
        Assert.AreEqual(40, persisted.State.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.InProgress, persisted.State.BackfillState);
        Assert.AreEqual(39, persisted.State.BackfillUpperSequence);
        Assert.AreEqual(12, persisted.State.BackfillCheckpoint);
        Assert.IsTrue(persisted.State.AuthorizationScope!.IsEquivalentTo(
            state.AuthorizationScope));
        Assert.HasCount(1, persisted.State.Enrollments);
        Assert.AreEqual(captureId, persisted.State.Enrollments[0].CaptureId);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Enrolled, persisted.State.Enrollments[0].State);
    }

    [TestMethod]
    public async Task ConcurrentWrite_ShouldReturnTheWinningSnapshot()
    {
        string dataFolder = AnalysisPersistenceTestData.CreateTestFolder();
        using LocalCaptureAnalysisControlStore store = CreateStore(dataFolder);
        CaptureAnalysisControlSnapshot initial = await store.GetAsync();
        CaptureAnalysisControlState winner = AnalysisPersistenceTestData.CreateControlState();
        CaptureAnalysisControlWriteResult first = await store.TryWriteAsync(
            winner,
            initial.DocumentRevision);

        CaptureAnalysisControlState staleCandidate = AnalysisPersistenceTestData.CreateControlState();
        CaptureAnalysisControlWriteResult conflict = await store.TryWriteAsync(
            staleCandidate,
            initial.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisControlWriteStatus.Succeeded, first.Status);
        Assert.AreEqual(CaptureAnalysisControlWriteStatus.Conflict, conflict.Status);
        Assert.AreSame(winner, conflict.Snapshot!.State);
        Assert.AreEqual(first.Snapshot!.DocumentRevision, conflict.Snapshot.DocumentRevision);
    }

    [TestMethod]
    public async Task InterruptedWrite_ShouldLeavePreviousCompleteLedger()
    {
        string dataFolder = AnalysisPersistenceTestData.CreateTestFolder();
        var writer = new InterruptingAtomicFileWriter();
        var protector = new TestDataProtectionService();
        using LocalCaptureAnalysisControlStore store = CreateStore(dataFolder, protector, writer);
        CaptureAnalysisControlSnapshot initial = await store.GetAsync();
        CaptureAnalysisControlState firstState = AnalysisPersistenceTestData.CreateControlState();
        CaptureAnalysisControlWriteResult first = await store.TryWriteAsync(
            firstState,
            initial.DocumentRevision);
        writer.InterruptNextWrite = true;
        CaptureAnalysisControlState stopped = new(
            firstState.Policy.StopFutureCaptures(currentSequence: 42),
            firstState.Enrollments);

        CaptureAnalysisControlWriteResult interrupted = await store.TryWriteAsync(
            stopped,
            first.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisControlWriteStatus.Unavailable, interrupted.Status);
        using LocalCaptureAnalysisControlStore reloaded = CreateStore(dataFolder, protector);
        CaptureAnalysisControlSnapshot persisted = await reloaded.GetAsync();
        Assert.AreEqual(first.Snapshot.DocumentRevision, persisted.DocumentRevision);
        Assert.IsTrue(persisted.State.IsFutureCaptureAdmissionEnabled);
    }

    [TestMethod]
    public async Task UnknownSchema_ShouldRemainFailClosedReadOnlyAndBytePreserved()
    {
        string dataFolder = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        using LocalCaptureAnalysisControlStore store = CreateStore(dataFolder, protector);
        string filePath = store.GetControlFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        byte[] original = protector.Protect(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":99,\"documentRevision\":7,\"futureState\":{\"keep\":true}}"));
        File.WriteAllBytes(filePath, original);

        CaptureAnalysisControlSnapshot snapshot = await store.GetAsync();
        CaptureAnalysisControlWriteResult write = await store.TryWriteAsync(
            AnalysisPersistenceTestData.CreateControlState(),
            snapshot.DocumentRevision);

        Assert.AreEqual(7, snapshot.DocumentRevision);
        Assert.AreEqual(CaptureAnalysisConsentState.Unknown, snapshot.State.ConsentState);
        Assert.AreEqual(CaptureAnalysisControlWriteStatus.ReadOnlyVersion, write.Status);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(filePath));
    }

    [TestMethod]
    public async Task UndecryptableLedger_ShouldFailClosedWithoutReplacingDurableIntent()
    {
        string dataFolder = AnalysisPersistenceTestData.CreateTestFolder();
        using LocalCaptureAnalysisControlStore store = CreateStore(dataFolder);
        string filePath = store.GetControlFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        byte[] corrupt = [0x01, 0x02, 0x03, 0x04];
        File.WriteAllBytes(filePath, corrupt);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await store.GetAsync());
        CaptureAnalysisControlWriteResult write = await store.TryWriteAsync(
            AnalysisPersistenceTestData.CreateControlState(),
            LocalCaptureAnalysisControlStore.InitialDocumentRevision);

        Assert.AreEqual(CaptureAnalysisControlWriteStatus.Unavailable, write.Status);
        CollectionAssert.AreEqual(corrupt, File.ReadAllBytes(filePath));
    }

    [TestMethod]
    public async Task DeletingAnalysisLocalCache_ShouldNotEraseExclusionsOrTombstones()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        string dataFolder = Path.Combine(root, "LocalState");
        string cacheFolder = Path.Combine(root, "LocalCache");
        CaptureId forgottenId = CaptureId.New();
        var forgotten = new CaptureAnalysisEnrollment(
            forgottenId,
            CaptureAnalysisEnrollmentState.Forgotten,
            CaptureAnalysisExclusionReason.SourceDeleted,
            enrollmentGeneration: 2,
            tombstoneGeneration: 3,
            assetFinalizationSequence: 12,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        CaptureAnalysisControlState state = AnalysisPersistenceTestData.CreateControlState(
            additionalEnrollment: forgotten);
        var protector = new TestDataProtectionService();
        using (LocalCaptureAnalysisControlStore store = CreateStore(dataFolder, protector))
        {
            CaptureAnalysisControlSnapshot initial = await store.GetAsync();
            Assert.AreEqual(
                CaptureAnalysisControlWriteStatus.Succeeded,
                (await store.TryWriteAsync(state, initial.DocumentRevision)).Status);
        }

        Directory.CreateDirectory(Path.Combine(cacheFolder, "CaptureAnalysis", "metadata-v1"));
        File.WriteAllText(
            Path.Combine(cacheFolder, "CaptureAnalysis", "metadata-v1", "disposable.cache"),
            "derived");
        Directory.Delete(Path.Combine(cacheFolder, "CaptureAnalysis"), recursive: true);

        using LocalCaptureAnalysisControlStore reloaded = CreateStore(dataFolder, protector);
        CaptureAnalysisControlSnapshot persisted = await reloaded.GetAsync();
        CaptureAnalysisEnrollment tombstone = persisted.State.Enrollments.Single(
            enrollment => enrollment.CaptureId == forgottenId);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Forgotten, tombstone.State);
        Assert.AreEqual(3, tombstone.TombstoneGeneration);
        Assert.AreEqual(CaptureAnalysisExclusionReason.SourceDeleted, tombstone.ExclusionReason);
    }

    private static LocalCaptureAnalysisControlStore CreateStore(
        string dataFolder,
        TestDataProtectionService? protector = null,
        IAtomicFileWriter? writer = null)
    {
        return new(
            new TestStorageService(dataFolder),
            protector ?? new TestDataProtectionService(),
            writer ?? new AtomicFileWriter(),
            new TestLogService());
    }
}
