using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis.Persistence;

[TestClass]
public sealed class LocalCaptureAnalysisStoreTests
{
    [TestMethod]
    public async Task WriteAndReload_ShouldPreserveTypedMetadataInProtectedOpaqueFile()
    {
        string root = Path.Combine(
            AnalysisPersistenceTestData.CreateTestFolder(),
            "SOURCE-PATH-CANARY-never-persist");
        var protector = new TestDataProtectionService();
        CaptureAnalysisRecord record = AnalysisPersistenceTestData.CreateRecord();
        using LocalCaptureAnalysisStore store = CreateStore(root, protector);

        CaptureAnalysisStoreWriteResult result = await store.TryWriteAsync(
            record,
            expectedDocumentRevision: null);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, result.Status);
        Assert.AreEqual(1, result.Snapshot!.DocumentRevision);
        string filePath = store.GetEnvelopeFilePath(record.CaptureId);
        Assert.IsTrue(File.Exists(filePath));
        Assert.DoesNotContain(record.CaptureId.ToString(), Path.GetFileName(filePath));
        Assert.HasCount(64, Path.GetFileNameWithoutExtension(filePath));

        byte[] protectedBytes = File.ReadAllBytes(filePath);
        string protectedText = Encoding.UTF8.GetString(protectedBytes);
        Assert.DoesNotContain("OCR-CANARY-海-12345", protectedText);
        byte[] plaintext = protector.Unprotect(protectedBytes);
        string json = Encoding.UTF8.GetString(plaintext);
        Assert.DoesNotContain("SOURCE-PATH-CANARY-never-persist", json);
        Assert.DoesNotContain("rawProvider", json, StringComparison.OrdinalIgnoreCase);

        using LocalCaptureAnalysisStore reloaded = CreateStore(root, protector);
        CaptureAnalysisStoreSnapshot? persisted = await reloaded.GetAsync(record.CaptureId);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(1, persisted.DocumentRevision);
        AnalysisPersistenceTestData.AssertRecordsEquivalent(record, persisted.Record);
    }

    [TestMethod]
    public async Task UnknownFutureCapability_ShouldRoundTripAsOpaqueJson()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAnalysisRecord record = AnalysisPersistenceTestData.CreateRecord();
        string filePath;
        using (LocalCaptureAnalysisStore store = CreateStore(root, protector))
        {
            Assert.AreEqual(
                CaptureAnalysisStoreWriteStatus.Succeeded,
                (await store.TryWriteAsync(record, null)).Status);
            filePath = store.GetEnvelopeFilePath(record.CaptureId);
        }

        byte[] plaintext = protector.Unprotect(File.ReadAllBytes(filePath));
        JsonObject rootObject = JsonNode.Parse(plaintext)!.AsObject();
        JsonObject futureRecipeCapability = JsonNode.Parse(
            """
            {
              "capability": {
                "id": "future-semantic-tags",
                "schemaVersion": 2,
                "classification": 3
              },
              "requirement": 2
            }
            """)!.AsObject();
        JsonObject futureEntry = JsonNode.Parse(
            """
            {
              "capability": {
                "id": "future-semantic-tags",
                "schemaVersion": 2,
                "classification": 3
              },
              "canonicalResult": {
                "futureModel": "v7",
                "structuredPayload": {
                  "weights": [1, 2.5, 9007199254740991],
                  "observedAtUtc": "2026-08-07T06:15:33.1230000+00:00",
                  "nested": { "keep": true }
                }
              }
            }
            """)!.AsObject();
        rootObject["recipe"]!["capabilities"]!.AsArray().Add((JsonNode?)futureRecipeCapability);
        rootObject["capabilityEntries"]!.AsArray().Add(futureEntry.DeepClone());
        File.WriteAllBytes(
            filePath,
            protector.Protect(Encoding.UTF8.GetBytes(rootObject.ToJsonString())));

        using (LocalCaptureAnalysisStore olderBuild = CreateStore(root, protector))
        {
            CaptureAnalysisStoreSnapshot? loaded = await olderBuild.GetAsync(record.CaptureId);
            Assert.IsNotNull(loaded);
            Assert.HasCount(4, loaded.Record.Recipe.Capabilities);
            Assert.HasCount(3, loaded.Record.Analyses);
            CaptureAnalysisStoreWriteResult rewritten = await olderBuild.TryWriteAsync(
                loaded.Record,
                loaded.DocumentRevision);
            Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, rewritten.Status);
            Assert.AreEqual(2, rewritten.Snapshot!.DocumentRevision);
        }

        JsonObject rewrittenRoot = JsonNode.Parse(
            protector.Unprotect(File.ReadAllBytes(filePath)))!.AsObject();
        JsonNode? preserved = rewrittenRoot["capabilityEntries"]!.AsArray()
            .Single(entry =>
                entry!["capability"]!["id"]!.GetValue<string>() == "future-semantic-tags");
        Assert.IsTrue(JsonNode.DeepEquals(futureEntry, preserved));
    }

    [TestMethod]
    public async Task UnknownEnvelopeVersion_ShouldRemainRetainedAndNeverOverwritten()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAnalysisRecord record = AnalysisPersistenceTestData.CreateRecord();
        using LocalCaptureAnalysisStore store = CreateStore(root, protector);
        string filePath = store.GetEnvelopeFilePath(record.CaptureId);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        byte[] original = protector.Protect(Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":99,\"documentRevision\":7,\"captureId\":\"{record.CaptureId}\",\"future\":true}}"));
        File.WriteAllBytes(filePath, original);

        CaptureAnalysisStoreSnapshot? loaded = await store.GetAsync(record.CaptureId);
        CaptureAnalysisStoreWriteResult write = await store.TryWriteAsync(record, null);

        Assert.IsNull(loaded);
        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.ReadOnlyVersion, write.Status);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(filePath));
        Assert.IsFalse(Directory.Exists(Path.Combine(
            root,
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            LocalCaptureAnalysisStore.MetadataVersionDirectoryName,
            LocalCaptureAnalysisStore.QuarantineDirectoryName)));
    }

    [TestMethod]
    public async Task CorruptEnvelope_ShouldBeQuarantinedWithoutHarmingOtherCaptures()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAnalysisRecord good = AnalysisPersistenceTestData.CreateRecord();
        CaptureAnalysisRecord corrupt = AnalysisPersistenceTestData.CreateRecord();
        using LocalCaptureAnalysisStore store = CreateStore(root, protector);
        Assert.AreEqual(
            CaptureAnalysisStoreWriteStatus.Succeeded,
            (await store.TryWriteAsync(good, null)).Status);
        string corruptPath = store.GetEnvelopeFilePath(corrupt.CaptureId);
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
        File.WriteAllBytes(corruptPath, [0x00, 0x11, 0x22, 0x33]);

        IReadOnlyList<CaptureAnalysisStoreSnapshot> snapshots = await ReadAllAsync(store);

        Assert.HasCount(1, snapshots);
        Assert.AreEqual(good.CaptureId, snapshots[0].Record.CaptureId);
        Assert.IsNotNull(await store.GetAsync(good.CaptureId));
        Assert.IsFalse(File.Exists(corruptPath));
        string quarantineDirectory = Path.Combine(
            root,
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            LocalCaptureAnalysisStore.MetadataVersionDirectoryName,
            LocalCaptureAnalysisStore.QuarantineDirectoryName);
        Assert.HasCount(1, Directory.GetFiles(quarantineDirectory, "*.corrupt"));
    }

    [TestMethod]
    public async Task InterruptedUpdate_ShouldLeavePreviousCompleteEnvelope()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        var protector = new TestDataProtectionService();
        var writer = new InterruptingAtomicFileWriter();
        CaptureAnalysisRecord firstRecord = AnalysisPersistenceTestData.CreateRecord();
        using LocalCaptureAnalysisStore store = CreateStore(root, protector, writer);
        CaptureAnalysisStoreWriteResult first = await store.TryWriteAsync(firstRecord, null);
        CaptureAnalysisRecord secondRecord = AnalysisPersistenceTestData.CreateRecord(
            firstRecord.CaptureId,
            fullText: "replacement text that must not partially commit");
        writer.InterruptNextWrite = true;

        CaptureAnalysisStoreWriteResult interrupted = await store.TryWriteAsync(
            secondRecord,
            first.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Unavailable, interrupted.Status);
        using LocalCaptureAnalysisStore reloaded = CreateStore(root, protector);
        CaptureAnalysisStoreSnapshot? persisted = await reloaded.GetAsync(firstRecord.CaptureId);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(1, persisted.DocumentRevision);
        Assert.AreEqual(
            "OCR-CANARY-海-12345",
            GetOcrPayload(persisted.Record).FullText);
    }

    [TestMethod]
    public async Task Delete_ShouldRequireTheCurrentMetadataRevision()
    {
        string root = AnalysisPersistenceTestData.CreateTestFolder();
        CaptureAnalysisRecord record = AnalysisPersistenceTestData.CreateRecord();
        using LocalCaptureAnalysisStore store = CreateStore(root);
        CaptureAnalysisStoreWriteResult written = await store.TryWriteAsync(record, null);

        CaptureAnalysisStoreWriteResult conflict = await store.TryDeleteAsync(
            record.CaptureId,
            expectedDocumentRevision: written.Snapshot!.DocumentRevision + 1);
        CaptureAnalysisStoreWriteResult deleted = await store.TryDeleteAsync(
            record.CaptureId,
            written.Snapshot.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Conflict, conflict.Status);
        Assert.AreEqual(written.Snapshot.DocumentRevision, conflict.Snapshot!.DocumentRevision);
        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, deleted.Status);
        Assert.IsNull(await store.GetAsync(record.CaptureId));
    }

    private static LocalCaptureAnalysisStore CreateStore(
        string localCacheFolder,
        TestDataProtectionService? protector = null,
        IAtomicFileWriter? writer = null)
    {
        return new(
            new TestLocalCachePathProvider(localCacheFolder),
            protector ?? new TestDataProtectionService(),
            writer ?? new AtomicFileWriter(),
            new TestLogService());
    }

    private static async Task<IReadOnlyList<CaptureAnalysisStoreSnapshot>> ReadAllAsync(
        LocalCaptureAnalysisStore store)
    {
        var results = new List<CaptureAnalysisStoreSnapshot>();
        await foreach (CaptureAnalysisStoreSnapshot snapshot in store.ReadAllAsync())
        {
            results.Add(snapshot);
        }

        return results;
    }

    private static OcrDocumentV1 GetOcrPayload(CaptureAnalysisRecord record)
    {
        Assert.IsTrue(record.TryGetAnalysis(
            AnalysisCapabilities.OcrDocumentV1.Id,
            out CapabilityAnalysis? analysis));
        return (OcrDocumentV1)analysis!.CanonicalResult!.Payload;
    }
}
