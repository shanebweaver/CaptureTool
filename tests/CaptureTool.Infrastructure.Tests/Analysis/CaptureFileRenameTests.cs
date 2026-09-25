using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureFileRenameTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    [DataRow(CaptureFileType.Image, ".png", false)]
    [DataRow(CaptureFileType.Audio, ".wav", false)]
    [DataRow(CaptureFileType.Video, ".mp4", true)]
    public async Task AcceptanceMovesTheFileAndUpdatesOnlyItsOwnPath(CaptureFileType kind, string extension, bool savedCopy)
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string source = Path.Combine(environment.Root, "source" + extension);
        string opened = savedCopy ? Path.Combine(environment.Root, "saved" + extension) : source;
        await File.WriteAllTextAsync(source, "retained capture", Ct);
        if (savedCopy) await File.WriteAllTextAsync(opened, "saved capture", Ct);
        var recent = new Recents(opened, kind);
        using var catalog = new LocalCaptureAssetCatalog(environment, environment.Protector, environment.Files, recent);
        var asset = new CaptureAsset(CaptureId.New(), kind, DateTimeOffset.UtcNow, source, CaptureSourceOwnership.Application,
            savedCopy ? opened : null);
        await catalog.RegisterAsync(asset, Ct);
        string renamed = await catalog.RenameFileAsync(asset.Id, opened, "Meeting notes" + extension, Ct);
        Assert.IsFalse(File.Exists(opened));
        Assert.AreEqual(savedCopy ? "saved capture" : "retained capture", await File.ReadAllTextAsync(renamed, Ct));
        var stored = (await catalog.GetAsync(asset.Id, Ct))!;
        Assert.AreEqual(savedCopy ? source : renamed, stored.SourcePath);
        Assert.AreEqual(savedCopy ? renamed : null, stored.PreferredPath);
        Assert.AreEqual(new CaptureName("Meeting notes", false), stored.Name);
        Assert.AreEqual(renamed, recent.GetEntries().Single().FilePath);
        if (savedCopy) Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task CollisionAndFailedIntentPublicationNeverMoveOrOverwriteFiles()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "capture.png"), conflict = Path.Combine(environment.Root, "existing.png");
        await File.WriteAllTextAsync(path, "capture", Ct);
        await File.WriteAllTextAsync(conflict, "unrelated", Ct);
        using var catalog = environment.CreateCatalog();
        var asset = new CaptureAsset(CaptureId.New(), CaptureFileType.Image, null, path, CaptureSourceOwnership.External);
        await catalog.RegisterAsync(asset, Ct);
        await Assert.ThrowsExactlyAsync<IOException>(() => catalog.RenameFileAsync(asset.Id, path, "existing.png", Ct));
        environment.Protector.FailProtection = true;
        await Assert.ThrowsExactlyAsync<System.Security.Cryptography.CryptographicException>(() => catalog.RenameFileAsync(asset.Id, path, "new.png", Ct));
        Assert.AreEqual("capture", await File.ReadAllTextAsync(path, Ct));
        Assert.AreEqual("unrelated", await File.ReadAllTextAsync(conflict, Ct));
        Assert.IsFalse(File.Exists(Path.Combine(environment.Root, "new.png")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RestartFinishesMetadataAndHistoryPublicationAfterTheMove(bool historyFails)
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "capture.png");
        await File.WriteAllTextAsync(path, "capture", Ct);
        var recent = new Recents(path, CaptureFileType.Image) { Fail = historyFails };
        var asset = new CaptureAsset(CaptureId.New(), CaptureFileType.Image, null, path, CaptureSourceOwnership.External);
        string renamed;
        using (var catalog = new LocalCaptureAssetCatalog(environment, environment.Protector, environment.Files, recent))
        {
            await catalog.RegisterAsync(asset, Ct);
            int writes = 0;
            environment.Files.BeforeWrite = (_, _) => !historyFails && Interlocked.Increment(ref writes) == 2
                ? Task.FromException(new IOException("Interrupted after move.")) : Task.CompletedTask;
            renamed = await catalog.RenameFileAsync(asset.Id, path, "Accepted.png", Ct);
            Assert.IsTrue(File.Exists(renamed));
            Assert.IsFalse(File.Exists(path));
        }
        recent.Fail = false;
        environment.Files.BeforeWrite = null;
        using var restarted = new LocalCaptureAssetCatalog(environment, environment.Protector, environment.Files, recent);
        Assert.AreEqual(renamed, (await restarted.GetAsync(asset.Id, Ct))!.SourcePath);
        Assert.AreEqual(renamed, recent.GetEntries().Single().FilePath);
        Assert.AreEqual("capture", await File.ReadAllTextAsync(renamed, Ct));
    }

    [TestMethod]
    public async Task RenameCanChangeOnlyLetterCase()
    {
        using var environment = new AnalysisTestEnvironment();
        Directory.CreateDirectory(environment.Root);
        string path = Path.Combine(environment.Root, "capture.png");
        await File.WriteAllTextAsync(path, "capture", Ct);
        using var catalog = environment.CreateCatalog();
        var asset = new CaptureAsset(CaptureId.New(), CaptureFileType.Image, null, path, CaptureSourceOwnership.External);
        await catalog.RegisterAsync(asset, Ct);
        string renamed = await catalog.RenameFileAsync(asset.Id, path, "Capture.png", Ct);
        Assert.Contains(renamed, Directory.GetFiles(environment.Root));
        Assert.AreEqual("Capture.png", Path.GetFileName((await catalog.GetAsync(asset.Id, Ct))!.SourcePath));
    }

    private sealed class Recents(string path, CaptureFileType type) : IRecentCaptureCatalog
    {
        private RecentCaptureCatalogEntry _entry = new(path, type, RecentCaptureOrigin.Captured, DateTime.UtcNow);
        public bool Fail;
        public IReadOnlyList<RecentCaptureCatalogEntry> GetEntries() => [_entry];
        public void ReplacePath(string oldPath, string newPath)
        {
            if (Fail) throw new IOException("History unavailable.");
            if (_entry.FilePath == oldPath) _entry = _entry with { FilePath = newPath };
        }
        public void RecordCaptured(string path, CaptureFileType type) => throw new NotSupportedException();
        public void RecordOpened(string path, CaptureFileType type) => throw new NotSupportedException();
        public void Touch(string path) => throw new NotSupportedException();
        public bool Remove(string path) => throw new NotSupportedException();
        public int RemoveRange(IEnumerable<string> paths) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}
