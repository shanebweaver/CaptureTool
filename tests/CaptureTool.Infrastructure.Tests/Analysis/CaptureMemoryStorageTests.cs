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
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(99)]
    public async Task UnsupportedPolicyVersionsAreRejectedWithoutMigration(int version)
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "CaptureMemoryPolicy.bin");
        byte[] bytes = environment.Protector.Protect(JsonSerializer.SerializeToUtf8Bytes(
            new CaptureMemoryPolicyDocument(version, new(true, Guid.NewGuid())),
            CaptureMemoryPolicyJsonContext.Default.CaptureMemoryPolicyDocument));
        await File.WriteAllBytesAsync(path, bytes, Ct);
        var store = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync(Ct));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path, Ct));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(99)]
    public async Task UnsupportedCatalogVersionsAreRejectedWithoutMigration(int version)
    {
        using var environment = new AnalysisTestEnvironment();
        string path = Path.Combine(environment.Root, "CaptureAssets", "catalog.bin");
        var documents = new ProtectedDocumentFile(environment.Protector, environment.Files);
        await documents.WriteAsync(path, new CaptureCatalogDocument(version, []),
            CaptureCatalogJsonContext.Default.CaptureCatalogDocument, Ct);
        byte[] bytes = await File.ReadAllBytesAsync(path, Ct);
        using var catalog = environment.CreateCatalog();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => catalog.ReadAllAsync(Ct));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path, Ct));
    }

    [TestMethod]
    public async Task PolicyRoundTripsProtectedAndFailedSaveKeepsLastCommittedPolicy()
    {
        using var environment = new AnalysisTestEnvironment();
        var store = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        Assert.IsNull(await store.LoadAsync(Ct));
        var policy = new CaptureMemoryPolicy(true, Guid.NewGuid());
        await store.SaveAsync(policy, Ct);
        var reopened = new LocalCaptureMemoryPolicyStore(environment, environment.Protector, environment.Files);
        Assert.AreEqual(policy, await reopened.LoadAsync(Ct));
        byte[] protectedBytes = await File.ReadAllBytesAsync(Path.Combine(environment.Root, "CaptureMemoryPolicy.bin"), Ct);
        Assert.DoesNotContain("ConsentGranted", System.Text.Encoding.UTF8.GetString(protectedBytes));
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<System.Security.Cryptography.CryptographicException>(() => store.SaveAsync(CaptureMemoryPolicy.Disabled(), Ct));
        Assert.AreEqual(policy, await reopened.LoadAsync(Ct));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.SaveAsync(new(false, Guid.Empty), Ct));
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
    public async Task HealthyEmptyControlIsNotDataButUnreadableControlKeepsDeleteAvailable()
    {
        using var environment = new AnalysisTestEnvironment();
        using var store = environment.CreateStore();
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
        await store.InitializeAsync(Ct);
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
        await File.WriteAllTextAsync(environment.ControlPath, "corrupt", Ct);
        Assert.AreEqual(new AnalysisStorageStatus(true, false), await store.GetStorageStatusAsync(Ct));
        await store.ClearAsync(Ct);
        Assert.AreEqual(new AnalysisStorageStatus(false, true), await store.GetStorageStatusAsync(Ct));
    }
}
