using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class LocalCaptureAssetCatalogTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Cancellation => TestContext.CancellationToken;

    [TestMethod]
    public async Task RegistrationAndAutoSavePreserveIdentityAndRetainedSourceAcrossRestart()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        CaptureAsset asset = Asset(environment, "retained.png");
        await catalog.RegisterAsync(asset, Cancellation);
        string exported = Path.Combine(environment.Root, "exported.png");
        await catalog.SetPreferredPathAsync(asset.Id, exported, Cancellation);
        await catalog.RegisterAsync(asset, Cancellation); // Replayed finalization must not undo auto-save.
        using LocalCaptureAssetCatalog reopened = environment.CreateCatalog();
        CaptureAsset reloaded = (await reopened.GetAsync(asset.Id, Cancellation))!;
        Assert.AreEqual(asset.Id, reloaded.Id);
        Assert.AreEqual(asset.SourcePath, reloaded.SourcePath);
        Assert.AreEqual(exported, reloaded.PreferredPath);
        Assert.HasCount(1, await reopened.ReadAllAsync(Cancellation));
        foreach (byte[] bytes in environment.Files.PublishedBytes)
            Assert.DoesNotContain("retained.png", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task ConflictingIdentityAndLocationUpdatesPreserveCatalog()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        CaptureAsset first = Asset(environment, "first.png");
        CaptureAsset second = Asset(environment, "second.png");
        await catalog.RegisterAsync(first, Cancellation);
        await catalog.RegisterAsync(second, Cancellation);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => catalog.RegisterAsync(first.RelocateSource(second.SourcePath), Cancellation));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => catalog.RegisterAsync(
            new CaptureAsset(CaptureId.New(), first.MediaType, first.CapturedAt, first.SourcePath.ToUpperInvariant(), first.SourceOwnership), Cancellation));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => catalog.RelocateSourceAsync(first.Id, second.SourcePath, Cancellation));
        Assert.AreEqual(first, await catalog.GetAsync(first.Id, Cancellation));
        Assert.AreEqual(second, await catalog.GetAsync(second.Id, Cancellation));
    }

    [TestMethod]
    public async Task ExplicitRelocationKeepsIdentityAndDoesNotMoveOrDeleteMedia()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        CaptureAsset asset = Asset(environment, "original.png");
        Directory.CreateDirectory(environment.Root);
        await File.WriteAllTextAsync(asset.SourcePath, "source media", Cancellation);
        await catalog.RegisterAsync(asset, Cancellation);
        string movedPath = Path.Combine(environment.Root, "moved.png");
        await catalog.RelocateSourceAsync(asset.Id, movedPath, Cancellation);
        Assert.AreEqual(movedPath, (await catalog.GetAsync(asset.Id, Cancellation))!.SourcePath);
        Assert.IsTrue(File.Exists(asset.SourcePath));
        Assert.IsFalse(File.Exists(movedPath));
    }

    [TestMethod]
    public async Task ClearingAnalysisPreservesCaptureCatalogAndMedia()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        using var metadata = environment.CreateStore();
        CaptureAsset asset = Asset(environment, "source.png");
        Directory.CreateDirectory(environment.Root);
        await File.WriteAllTextAsync(asset.SourcePath, "source media", Cancellation);
        await catalog.RegisterAsync(asset, Cancellation);
        AnalysisWriteToken token = await metadata.BeginRunAsync(asset.Id, AnalysisMediaKind.Image, AnalysisTestEnvironment.Revision(), "v1", Cancellation);
        await metadata.TryWriteAsync(token, AnalysisTestEnvironment.Description(), Cancellation);
        await metadata.ClearAsync(Cancellation);
        using LocalCaptureAssetCatalog reopened = environment.CreateCatalog();
        Assert.AreEqual(asset, await reopened.GetAsync(asset.Id, Cancellation));
        Assert.AreEqual("source media", await File.ReadAllTextAsync(asset.SourcePath, Cancellation));
    }

    [TestMethod]
    public async Task ProtectionAndPublicationFailuresDoNotPartiallyUpdateCaptureLocations()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        CaptureAsset asset = Asset(environment, "source.png");
        await catalog.RegisterAsync(asset, Cancellation);
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<CryptographicException>(() => catalog.SetPreferredPathAsync(asset.Id, Path.Combine(environment.Root, "export.png"), Cancellation));
        environment.Protector.FailProtection = false;
        environment.Files.BeforeWrite = (_, _) => throw new IOException("Injected write failure.");
        await Assert.ThrowsExactlyAsync<IOException>(() => catalog.RelocateSourceAsync(asset.Id, Path.Combine(environment.Root, "moved.png"), Cancellation));
        Assert.AreEqual(asset, await catalog.GetAsync(asset.Id, Cancellation));
    }

    [TestMethod]
    public async Task UnknownCatalogSchemaIsNotResetOnRegistration()
    {
        using var environment = new AnalysisTestEnvironment();
        using LocalCaptureAssetCatalog catalog = environment.CreateCatalog();
        await catalog.RegisterAsync(Asset(environment, "first.png"), Cancellation);
        string path = Path.Combine(environment.Root, "CaptureAssets", "catalog.bin");
        JsonNode node = JsonNode.Parse(environment.Protector.Unprotect(await File.ReadAllBytesAsync(path, Cancellation)))!;
        node["Version"] = 99;
        byte[] unknown = environment.Protector.Protect(Encoding.UTF8.GetBytes(node.ToJsonString()));
        await File.WriteAllBytesAsync(path, unknown, Cancellation);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => catalog.RegisterAsync(Asset(environment, "second.png"), Cancellation));
        CollectionAssert.AreEqual(unknown, await File.ReadAllBytesAsync(path, Cancellation));
    }

    [TestMethod]
    public void CompositionSharesOneStoreAndDoesNotCreateDataUntilUsed()
    {
        using var environment = new AnalysisTestEnvironment();
        var services = new ServiceCollection();
        services.AddGenericServices();
        services.AddSingleton<IStorageService>(environment);
        services.AddSingleton<IUserDataProtector>(environment.Protector);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.AreSame(provider.GetRequiredService<ICaptureAnalysisStore>(), provider.GetRequiredService<ICaptureMetadataReader>());
        Assert.AreSame(provider.GetRequiredService<ICaptureAssetCatalog>(), provider.GetRequiredService<ICaptureAssetCatalog>());
        Assert.IsFalse(Directory.Exists(environment.Root));
    }

    private static CaptureAsset Asset(AnalysisTestEnvironment environment, string name) =>
        new(CaptureId.New(), CaptureFileType.Image, DateTimeOffset.UtcNow, Path.Combine(environment.Root, name), CaptureSourceOwnership.Application);
}
