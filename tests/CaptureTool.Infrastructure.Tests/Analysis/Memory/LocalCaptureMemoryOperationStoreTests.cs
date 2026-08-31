using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Domain;
using CaptureTool.Infrastructure.Analysis.Memory;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Tests.Analysis.Persistence;
using System.Text;

namespace CaptureTool.Infrastructure.Tests.Analysis.Memory;

[TestClass]
public sealed class LocalCaptureMemoryOperationStoreTests
{
    [TestMethod]
    public async Task Journal_ShouldRoundTripProtectedIntentAndEnforceRevision()
    {
        string folder = AnalysisPersistenceTestData.CreateTestFolder();
        var protection = new TestDataProtectionService();
        var operation = CreateOperation();
        using var store = new LocalCaptureMemoryOperationStore(new TestStorageService(folder), protection, new AtomicFileWriter());
        Assert.AreEqual(0, (await store.GetAsync()).Revision);
        Assert.IsTrue(await store.TryWriteAsync(operation, 0));
        Assert.IsFalse(await store.TryWriteAsync(operation.Advance(CaptureMemoryOperationPhase.PreparingModels), 0));
        string protectedText = Encoding.UTF8.GetString(File.ReadAllBytes(store.FilePath));
        Assert.DoesNotContain(operation.CaptureIds[0].ToString(), protectedText);

        using var restarted = new LocalCaptureMemoryOperationStore(new TestStorageService(folder), protection, new AtomicFileWriter());
        var loaded = await restarted.GetAsync();
        Assert.AreEqual(1, loaded.Revision);
        Assert.AreEqual(operation.Id, loaded.Operation!.Id);
        Assert.AreEqual(operation.Request, loaded.Operation.Request);
        Assert.AreEqual(operation.ControlGeneration, loaded.Operation.ControlGeneration);
        CollectionAssert.AreEqual(operation.CaptureIds.ToArray(), loaded.Operation.CaptureIds.ToArray());
    }

    [TestMethod]
    public async Task FailedReplace_ShouldKeepTheLastDurableIntent()
    {
        string folder = AnalysisPersistenceTestData.CreateTestFolder();
        var writer = new InterruptingAtomicFileWriter();
        var protection = new TestDataProtectionService();
        using var store = new LocalCaptureMemoryOperationStore(new TestStorageService(folder), protection, writer);
        var operation = CreateOperation();
        Assert.IsTrue(await store.TryWriteAsync(operation, 0));
        writer.InterruptNextWrite = true;
        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await store.TryWriteAsync(operation.Advance(CaptureMemoryOperationPhase.PreparingModels), 1));
        Assert.AreEqual(CaptureMemoryOperationPhase.Accepted, (await store.GetAsync()).Operation!.Phase);
        using var restarted = new LocalCaptureMemoryOperationStore(new TestStorageService(folder), protection, writer);
        Assert.AreEqual(CaptureMemoryOperationPhase.Accepted, (await restarted.GetAsync()).Operation!.Phase);
    }

    [TestMethod]
    public async Task CorruptJournal_ShouldNotBeOverwrittenOrSilentlyTreatedAsNoPendingWork()
    {
        string folder = AnalysisPersistenceTestData.CreateTestFolder();
        using var store = new LocalCaptureMemoryOperationStore(new TestStorageService(folder),
            new TestDataProtectionService(), new AtomicFileWriter());
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        byte[] corrupt = [1, 2, 3];
        File.WriteAllBytes(store.FilePath, corrupt);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await store.GetAsync());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await store.TryWriteAsync(CreateOperation(), 0));
        CollectionAssert.AreEqual(corrupt, File.ReadAllBytes(store.FilePath));
    }

    private static CaptureMemoryOperation CreateOperation() => new(Guid.NewGuid(),
        new(CaptureMemoryOperationKind.Reanalyze), DateTimeOffset.UtcNow, 3, 5,
        CaptureMemoryOperationPhase.Accepted, CaptureMemoryOperationStatus.Running, [CaptureId.New()]);
}
