using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using CaptureTool.Infrastructure.Persistence;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureMemoryStorageTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    public async Task NarrowerV1ConsentMigratesOnceWithoutAuthorizingAnyAiFeature()
    {
        using var environment = new AnalysisTestEnvironment();
        var documents = new ProtectedDocumentFile(environment.Protector, environment.Files);
        string path = Path.Combine(environment.Root, "CaptureMemoryPolicy.bin");
        var old = new CaptureMemoryPolicy(true, true, Guid.NewGuid(), 12);
        await documents.WriteAsync(path, new CaptureMemoryPolicyDocument(1, old), CaptureMemoryPolicyJsonContext.Default.CaptureMemoryPolicyDocument, Ct);
        var store = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        var migrated = await store.LoadAsync(Ct);
        Assert.IsNotNull(migrated);
        Assert.IsFalse(migrated.ConsentGranted);
        Assert.IsFalse(migrated.ScanningEnabled);
        Assert.AreEqual(old.EnableBoundary, migrated.EnableBoundary);
        Assert.AreNotEqual(old.Revision, migrated.Revision);
        Assert.AreEqual(migrated, await store.LoadAsync(Ct));
        var persisted = await documents.ReadAsync(path, CaptureMemoryPolicyJsonContext.Default.CaptureMemoryPolicyDocument, Ct);
        Assert.AreEqual(2, persisted!.Version);
    }

    [TestMethod]
    public async Task PolicyRoundTripsProtectedAndFailedSaveKeepsLastCommittedPolicy()
    {
        using var environment = new AnalysisTestEnvironment();
        var store = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        Assert.IsNull(await store.LoadAsync(Ct));
        var policy = new CaptureMemoryPolicy(true, true, Guid.NewGuid(), 12);
        await store.SaveAsync(policy, Ct);
        var reopened = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        Assert.AreEqual(policy, await reopened.LoadAsync(Ct));
        byte[] protectedBytes = await File.ReadAllBytesAsync(Path.Combine(environment.Root, "CaptureMemoryPolicy.bin"), Ct);
        Assert.DoesNotContain("ScanningEnabled", System.Text.Encoding.UTF8.GetString(protectedBytes));
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<System.Security.Cryptography.CryptographicException>(() => store.SaveAsync(CaptureMemoryPolicy.Disabled(), Ct));
        Assert.AreEqual(policy, await reopened.LoadAsync(Ct));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(new(true, false, Guid.NewGuid(), 0), Ct));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(new(false, false, Guid.Empty, 0), Ct));
    }

    [TestMethod]
    public async Task UnreadablePolicyIsNotReplacedWithDefaultConsent()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "CaptureMemoryPolicy.bin");
        await File.WriteAllTextAsync(path, "unreadable", Ct);
        var store = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        await Assert.ThrowsExactlyAsync<System.Security.Cryptography.CryptographicException>(() => store.LoadAsync(Ct));
        Assert.AreEqual("unreadable", await File.ReadAllTextAsync(path, Ct));
    }

    [TestMethod]
    public async Task V1CatalogMigrationPersistsHistoricalOrderAndReplayCannotChangeEligibility()
    {
        using var environment = new AnalysisTestEnvironment();
        var document = new CaptureCatalogDocument(1, [
            new(Guid.NewGuid(), (int)CaptureFileType.Image, DateTimeOffset.UtcNow, Path.Combine(environment.Root, "old.png"), (int)CaptureSourceOwnership.Application, null)
        ]);
        var documents = new ProtectedDocumentFile(environment.Protector, environment.Files);
        string path = Path.Combine(environment.Root, "CaptureAssets", "catalog.bin");
        await documents.WriteAsync(path, document, CaptureCatalogJsonContext.Default.CaptureCatalogDocument, Ct);
        using var catalog = environment.CreateCatalog();
        var entry = (await catalog.ReadRegistrationsAsync(Ct)).Single();
        Assert.AreEqual(1L, entry.Sequence);
        Assert.IsNull(entry.AutomaticAuthorization);
        await catalog.RegisterForAnalysisAsync(entry.Asset, Guid.NewGuid(), Ct);
        using var reopened = environment.CreateCatalog();
        Assert.AreEqual(entry, (await reopened.ReadRegistrationsAsync(Ct)).Single());
        Assert.AreEqual(1L, await reopened.GetBoundaryAsync(Ct));
        Guid authorization = Guid.NewGuid();
        var next = new CaptureAsset(CaptureId.New(), CaptureFileType.Audio, DateTimeOffset.UtcNow, Path.Combine(environment.Root, "next.wav"), CaptureSourceOwnership.Application);
        await reopened.RegisterForAnalysisAsync(next, authorization, Ct);
        Assert.AreEqual(2L, await reopened.GetBoundaryAsync(Ct));
        var added = (await reopened.ReadRegistrationsAsync(Ct)).Single(item => item.Asset.Id == next.Id);
        Assert.AreEqual(authorization, added.AutomaticAuthorization);
        Assert.AreEqual(2L, added.Sequence);
        var persisted = JsonSerializer.Deserialize(environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Ct)), CaptureCatalogJsonContext.Default.CaptureCatalogDocument);
        Assert.AreEqual(2, persisted!.Version);
    }

    [TestMethod]
    public async Task HealthyEmptyControlIsNotDataButUnreadableControlKeepsDeleteAvailable()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
        await store.InitializeAsync(Ct);
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
        await File.WriteAllTextAsync(environment.ControlPath, "corrupt", Ct);
        Assert.AreEqual(new AnalysisStorageStatus(true, false), await store.GetStorageStatusAsync(Ct));
        await store.ClearAsync(0, Ct);
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
    }
}
