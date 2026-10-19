using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Sources;
using System.Text;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureMemoryIntegrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SavedCopyAnalysisUsesRequestedContentsEvenWithoutRetainedOriginal(bool removeOriginal)
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        var saved = app.Asset();
        await File.WriteAllTextAsync(saved.SourcePath, "edited saved copy", Ct);
        await app.Memory.RegisterCaptureAsync(asset, Ct);
        await app.Memory.SetPreferredPathAsync(asset.SourcePath, saved.SourcePath, Ct);
        if (removeOriginal) File.Delete(asset.SourcePath);

        await app.Memory.AnalyzeAsync(saved.SourcePath, AnalysisCapability.Description, Ct);
        await app.CompletedAsync(asset, Ct);

        Assert.AreEqual(saved.SourcePath, app.Analyzer.LastSourcePath);
        Assert.AreEqual("edited saved copy", app.Analyzer.LastSourceContents);
        await using var source = await new LocalAnalysisSource().OpenAsync(saved.SourcePath, Ct);
        Assert.AreEqual(source.Revision, (await app.Store.GetAsync(asset.Id, cancellationToken: Ct))!.SourceRevision);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("unsupported")]
    [DataRow("corrupt")]
    public async Task DamagedControlKeepsExplicitDeletionAvailableAfterInitializationAndRefresh(string damage)
    {
        using var environment = new AnalysisTestEnvironment();
        await using (var app = new App(environment))
        {
            await app.EnableAsync(Ct);
            var asset = app.Asset();
            await app.RequestAsync(asset, Ct);
            await app.CompletedAsync(asset, Ct);
        }
        if (damage == "missing") File.Delete(environment.ControlPath);
        else if (damage == "corrupt") await File.WriteAllTextAsync(environment.ControlPath, "corrupt", Ct);
        else
        {
            var document = JsonNode.Parse(environment.Protector.Unprotect(await File.ReadAllBytesAsync(environment.ControlPath, Ct)))!;
            document["Version"] = 99;
            await File.WriteAllBytesAsync(environment.ControlPath, environment.Protector.Protect(Encoding.UTF8.GetBytes(document.ToJsonString())), Ct);
        }

        await using var restarted = new App(environment);
        await restarted.Memory.InitializeAsync(Ct);
        Assert.IsTrue(restarted.Memory.State.CanDelete);
        await restarted.Memory.RefreshAsync(Ct);
        Assert.IsTrue(restarted.Memory.State.CanDelete);
        restarted.Prompts.Delete = true;
        await restarted.Memory.DeleteMetadataAsync(Ct);
        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsFalse(restarted.Memory.State.CanDelete);
        Assert.IsTrue((await restarted.Store.GetStorageStatusAsync(Ct)).IsAvailable);
    }

    [TestMethod]
    public async Task UnreadableCaptureCatalogCannotPreventDeletingAnalysisMetadata()
    {
        using var environment = new AnalysisTestEnvironment();
        await using var app = new App(environment);
        await app.EnableAsync(Ct);
        var asset = app.Asset();
        await app.RequestAsync(asset, Ct);
        await app.CompletedAsync(asset, Ct);
        string catalog = Path.Combine(environment.Root, "CaptureAssets", "catalog.bin");
        await File.WriteAllTextAsync(catalog, "corrupt", Ct);

        app.Prompts.Delete = true;
        await app.Memory.DeleteMetadataAsync(Ct);

        Assert.IsEmpty(environment.MetadataPaths);
        Assert.IsFalse(app.Memory.State.CanDelete);
        Assert.AreEqual("corrupt", await File.ReadAllTextAsync(catalog, Ct));
        Assert.IsTrue(File.Exists(asset.SourcePath));
    }
}
