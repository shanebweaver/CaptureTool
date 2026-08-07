using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CaptureAssetBootstrapperTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task InitializeAsync_ShouldMigrateCapturedHistoryOnceAndIgnoreOpenedHistory()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "legacy", "captured.png");
        string openedPath = Path.Combine(dataFolder, "legacy", "opened.png");
        var recentCatalog = new TestRecentCaptureCatalog(
        [
            new(capturedPath, CaptureFileType.Image, RecentCaptureOrigin.Captured, UtcNow),
            new(openedPath, CaptureFileType.Image, RecentCaptureOrigin.Opened, UtcNow.AddMinutes(-1)),
        ]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await bootstrapper.InitializeAsync(TestContext.CancellationToken);
        await bootstrapper.InitializeAsync(TestContext.CancellationToken);

        Assert.HasCount(1, assetCatalog.Assets);
        CaptureAsset asset = assetCatalog.Assets[0];
        Assert.AreEqual(Path.GetFullPath(capturedPath), asset.RetainedSourcePath);
        Assert.AreEqual(Path.GetFullPath(capturedPath), asset.PreferredOpenPath);
        Assert.AreEqual(CaptureSourceOwnership.LegacyExternal, asset.SourceOwnership);
        Assert.HasCount(1, assetCatalog.Changes);

        RecentCaptureCatalogEntry captured = recentCatalog.Entries.Single(entry =>
            entry.Origin == RecentCaptureOrigin.Captured);
        RecentCaptureCatalogEntry opened = recentCatalog.Entries.Single(entry =>
            entry.Origin == RecentCaptureOrigin.Opened);
        Assert.AreEqual(asset.Id, captured.CaptureId);
        Assert.IsNull(opened.CaptureId);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldRecoverOrphanFromRetainedFolderWithoutReadingContent()
    {
        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        Directory.CreateDirectory(retainedFolder);
        string retainedPath = Path.Combine(retainedFolder, "orphan.png");
        await File.WriteAllTextAsync(retainedPath, "content must remain unread", TestContext.CancellationToken);
        var recentCatalog = new TestRecentCaptureCatalog([]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await using (var lockedFile = new FileStream(
            retainedPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            await bootstrapper.InitializeAsync(TestContext.CancellationToken);
        }

        Assert.HasCount(1, assetCatalog.Assets);
        CaptureAsset asset = assetCatalog.Assets[0];
        Assert.AreEqual(Path.GetFullPath(retainedPath), asset.RetainedSourcePath);
        Assert.AreEqual(CaptureSourceOwnership.AppOwned, asset.SourceOwnership);
        Assert.IsNull(asset.PreferredOpenPath);
        Assert.AreEqual(asset.Id, recentCatalog.Entries.Single().CaptureId);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldNotTurnOpenedRetainedFileIntoAsset()
    {
        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        Directory.CreateDirectory(retainedFolder);
        string openedPath = Path.Combine(retainedFolder, "opened.png");
        await File.WriteAllTextAsync(openedPath, "opened", TestContext.CancellationToken);
        var recentCatalog = new TestRecentCaptureCatalog(
        [
            new(openedPath, CaptureFileType.Image, RecentCaptureOrigin.Opened, UtcNow),
        ]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await bootstrapper.InitializeAsync(TestContext.CancellationToken);

        Assert.IsEmpty(assetCatalog.Assets);
        Assert.IsNull(recentCatalog.Entries.Single().CaptureId);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldHonorClearedOpenedPathExclusion()
    {
        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        Directory.CreateDirectory(retainedFolder);
        string openedPath = Path.Combine(retainedFolder, "opened-then-cleared.png");
        await File.WriteAllTextAsync(openedPath, "opened", TestContext.CancellationToken);
        var recentCatalog = new TestRecentCaptureCatalog([]);
        recentCatalog.RecoveryExclusions.Add(Path.GetFullPath(openedPath));
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await bootstrapper.InitializeAsync(TestContext.CancellationToken);

        Assert.IsEmpty(assetCatalog.Assets);
        Assert.IsEmpty(recentCatalog.Entries);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldTreatRetainedFileLinkAsLegacyExternalDuringMigration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        string externalFolder = Path.Combine(dataFolder, "External");
        Directory.CreateDirectory(retainedFolder);
        Directory.CreateDirectory(externalFolder);
        string externalPath = Path.Combine(externalFolder, "external.png");
        string linkedPath = Path.Combine(retainedFolder, "linked.png");
        await File.WriteAllTextAsync(
            externalPath,
            "external content must remain unread",
            TestContext.CancellationToken);
        if (!TryCreateFileSymbolicLink(linkedPath, externalPath))
        {
            return;
        }

        var recentCatalog = new TestRecentCaptureCatalog(
        [
            new(linkedPath, CaptureFileType.Image, RecentCaptureOrigin.Captured, UtcNow),
        ]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await using (var lockedFile = new FileStream(
            externalPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            await bootstrapper.InitializeAsync(TestContext.CancellationToken);
        }

        CaptureAsset asset = assetCatalog.Assets.Single();
        Assert.AreEqual(Path.GetFullPath(linkedPath), asset.RetainedSourcePath);
        Assert.AreEqual(CaptureSourceOwnership.LegacyExternal, asset.SourceOwnership);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldSkipRetainedFileLinkDuringOrphanRecovery()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        string externalFolder = Path.Combine(dataFolder, "External");
        Directory.CreateDirectory(retainedFolder);
        Directory.CreateDirectory(externalFolder);
        string externalPath = Path.Combine(externalFolder, "external.png");
        string linkedPath = Path.Combine(retainedFolder, "linked.png");
        await File.WriteAllTextAsync(
            externalPath,
            "external content must remain unread",
            TestContext.CancellationToken);
        if (!TryCreateFileSymbolicLink(linkedPath, externalPath))
        {
            return;
        }

        var recentCatalog = new TestRecentCaptureCatalog([]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await using (var lockedFile = new FileStream(
            externalPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            await bootstrapper.InitializeAsync(TestContext.CancellationToken);
        }

        Assert.IsEmpty(assetCatalog.Assets);
        Assert.IsEmpty(recentCatalog.Entries);
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldRejectRetainedFolderLinkForMigrationAndRecovery()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dataFolder = CreateTestFolder();
        string externalFolder = Path.Combine(dataFolder, "ExternalCaptures");
        Directory.CreateDirectory(externalFolder);
        string historyTargetPath = Path.Combine(externalFolder, "history.png");
        string orphanTargetPath = Path.Combine(externalFolder, "orphan.png");
        await File.WriteAllTextAsync(historyTargetPath, "history", TestContext.CancellationToken);
        await File.WriteAllTextAsync(orphanTargetPath, "orphan", TestContext.CancellationToken);
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        if (!TryCreateDirectorySymbolicLink(retainedFolder, externalFolder))
        {
            return;
        }

        string historyLinkedPath = Path.Combine(retainedFolder, "history.png");
        var recentCatalog = new TestRecentCaptureCatalog(
        [
            new(historyLinkedPath, CaptureFileType.Image, RecentCaptureOrigin.Captured, UtcNow),
        ]);
        var assetCatalog = new TestCaptureAssetCatalog();
        var bootstrapper = CreateBootstrapper(dataFolder, assetCatalog, recentCatalog);

        await bootstrapper.InitializeAsync(TestContext.CancellationToken);

        CaptureAsset asset = assetCatalog.Assets.Single();
        Assert.AreEqual(Path.GetFullPath(historyLinkedPath), asset.RetainedSourcePath);
        Assert.AreEqual(CaptureSourceOwnership.LegacyExternal, asset.SourceOwnership);
        Assert.IsFalse(
            assetCatalog.Assets.Any(candidate => string.Equals(
                candidate.RetainedSourcePath,
                Path.Combine(retainedFolder, "orphan.png"),
                StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task InitializeAsync_ShouldRepairPendingProjectionAndIssueReplacementWake()
    {
        string dataFolder = CreateTestFolder();
        var recentCatalog = new TestRecentCaptureCatalog([]);
        var assetCatalog = new TestCaptureAssetCatalog();
        CaptureAsset asset = CaptureAsset.Create(
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "pending.png"),
            CaptureSourceOwnership.AppOwned,
            new DateTimeOffset(UtcNow));
        Assert.IsTrue(assetCatalog.TryAdd(asset).Succeeded);
        var changeSignal = new TestChangeSignal();
        CaptureAssetBootstrapper bootstrapper = CreateBootstrapper(
            dataFolder,
            assetCatalog,
            recentCatalog,
            changeSignal);

        await bootstrapper.InitializeAsync(TestContext.CancellationToken);

        Assert.AreEqual(asset.Id, recentCatalog.Entries.Single().CaptureId);
        Assert.AreEqual(1, changeSignal.SignalCount);
    }

    private static CaptureAssetBootstrapper CreateBootstrapper(
        string dataFolder,
        TestCaptureAssetCatalog assetCatalog,
        TestRecentCaptureCatalog recentCatalog,
        TestChangeSignal? changeSignal = null)
    {
        return new(
            assetCatalog,
            recentCatalog,
            changeSignal ?? new TestChangeSignal(),
            new TestStorageService(dataFolder),
            new TestClock(),
            new TestLogService());
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            _ = File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private sealed class TestCaptureAssetCatalog : ICaptureAssetCatalog
    {
        public List<CaptureAsset> Assets { get; } = [];
        public List<CaptureAssetChange> Changes { get; } = [];

        public IReadOnlyList<CaptureAsset> GetAssets() => [.. Assets];

        public CaptureAsset? Get(CaptureId captureId) =>
            Assets.Find(asset => asset.Id == captureId);

        public CaptureAsset? FindByPath(string filePath) =>
            Assets.Find(asset =>
                asset.LifecycleState == CaptureAssetLifecycleState.Active &&
                (PathsEqual(asset.RetainedSourcePath, filePath) ||
                 PathsEqual(asset.PreferredOpenPath, filePath)));

        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) =>
            Changes.Where(change => change.Sequence > sequence).ToArray();

        public long GetLatestChangeSequence() => Changes.Count == 0 ? 0 : Changes[^1].Sequence;

        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset) =>
            TryAddRange([asset])[0];

        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets)
        {
            List<CaptureAssetCatalogWriteResult> results = [];
            foreach (CaptureAsset candidate in assets)
            {
                CaptureAsset? existing = Get(candidate.Id) ?? FindByPath(candidate.RetainedSourcePath);
                if (existing is not null)
                {
                    long sequence = Changes.First(change =>
                        change.CaptureId == existing.Id &&
                        change.ChangeType == CaptureAssetChangeType.Finalized).Sequence;
                    results.Add(CaptureAssetCatalogWriteResult.Unchanged(existing, sequence));
                    continue;
                }

                long nextSequence = GetLatestChangeSequence() + 1;
                Assets.Add(candidate);
                Changes.Add(new(
                    nextSequence,
                    candidate.Id,
                    candidate.LifecycleRevision,
                    CaptureAssetChangeType.Finalized,
                    new DateTimeOffset(UtcNow)));
                results.Add(CaptureAssetCatalogWriteResult.Committed(candidate, nextSequence));
            }

            return results;
        }

        public CaptureAssetCatalogWriteResult TryUpdate(
            CaptureAsset asset,
            long expectedLifecycleRevision,
            CaptureAssetChangeType changeType)
        {
            int index = Assets.FindIndex(existing => existing.Id == asset.Id);
            if (index < 0 || Assets[index].LifecycleRevision != expectedLifecycleRevision)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            long sequence = GetLatestChangeSequence() + 1;
            Assets[index] = asset;
            Changes.Add(new(
                sequence,
                asset.Id,
                asset.LifecycleRevision,
                changeType,
                new DateTimeOffset(UtcNow)));
            return CaptureAssetCatalogWriteResult.Committed(asset, sequence);
        }

        private static bool PathsEqual(string? left, string? right) =>
            string.Equals(
                left is null ? null : Path.GetFullPath(left),
                right is null ? null : Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestRecentCaptureCatalog(List<RecentCaptureCatalogEntry> entries) :
        IRecentCaptureCatalog
    {
        private long _checkpoint;

        public List<RecentCaptureCatalogEntry> Entries { get; } = entries;
        public HashSet<string> RecoveryExclusions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RecentCaptureCatalogEntry> GetEntries() => [.. Entries];
        public long GetCaptureAssetChangeCheckpoint() => _checkpoint;
        public bool IsRetainedCaptureRecoveryExcluded(string filePath) =>
            RecoveryExclusions.Contains(Path.GetFullPath(filePath));

        public void RecordCaptured(string filePath, CaptureFileType captureFileType) =>
            Entries.Add(new(filePath, captureFileType, RecentCaptureOrigin.Captured, UtcNow));

        public void RecordCaptured(
            string filePath,
            CaptureFileType captureFileType,
            CaptureId captureId) =>
            Entries.Add(new(filePath, captureFileType, RecentCaptureOrigin.Captured, UtcNow, captureId));

        public void RecordOpened(string filePath, CaptureFileType captureFileType) =>
            Entries.Add(new(filePath, captureFileType, RecentCaptureOrigin.Opened, UtcNow));

        public bool TryProjectCaptured(
            string filePath,
            CaptureFileType captureFileType,
            CaptureId captureId,
            long changeSequence,
            DateTime activityUtc)
        {
            int index = Entries.FindIndex(entry => entry.CaptureId == captureId);
            if (changeSequence <= _checkpoint && index < 0)
            {
                return true;
            }

            var projected = new RecentCaptureCatalogEntry(
                Path.GetFullPath(filePath),
                captureFileType,
                RecentCaptureOrigin.Captured,
                activityUtc,
                captureId);
            if (index >= 0)
            {
                Entries[index] = projected;
            }
            else
            {
                int pathIndex = Entries.FindIndex(entry => PathsEqual(entry.FilePath, filePath));
                if (pathIndex >= 0)
                {
                    Entries[pathIndex] = projected;
                }
                else
                {
                    Entries.Add(projected);
                }
            }

            if (changeSequence == _checkpoint + 1)
            {
                _checkpoint = changeSequence;
            }

            return true;
        }

        public bool TryAdvanceCaptureAssetChangeCheckpoint(long changeSequence)
        {
            _checkpoint = Math.Max(_checkpoint, changeSequence);
            return true;
        }

        public bool TryAssignCaptureId(string filePath, CaptureId captureId)
        {
            int index = Entries.FindIndex(entry =>
                entry.Origin == RecentCaptureOrigin.Captured &&
                entry.CaptureId is null &&
                PathsEqual(entry.FilePath, filePath));
            if (index < 0)
            {
                return false;
            }

            Entries[index] = Entries[index] with { CaptureId = captureId };
            return true;
        }

        public bool TryRepairCapturedProjection(
            string oldFilePath,
            string newFilePath,
            CaptureFileType captureFileType,
            CaptureId captureId,
            DateTime activityUtc) => false;
        public void ReplacePath(string oldFilePath, string newFilePath) { }
        public void Touch(string filePath) { }
        public bool Remove(string filePath) => false;
        public int RemoveRange(IEnumerable<string> filePaths) => 0;
        public void Clear() => Entries.Clear();

        public void Clear(long throughChangeSequence)
        {
            Entries.Clear();
            _checkpoint = Math.Max(_checkpoint, throughChangeSequence);
        }

        private static bool PathsEqual(string left, string right) =>
            string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestStorageService(string dataFolder) : IStorageService
    {
        public string GetApplicationDataFolderPath() => dataFolder;
        public string GetApplicationRetainedCaptureFolderPath() => Path.Combine(dataFolder, "Captures");
        public string GetApplicationScratchFolderPath() => Path.Combine(dataFolder, "Scratch");
        public string GetSystemDefaultMusicFolderPath() => Path.Combine(dataFolder, "Music");
        public string GetSystemDefaultScreenshotsFolderPath() => Path.Combine(dataFolder, "Pictures");
        public string GetSystemDefaultVideosFolderPath() => Path.Combine(dataFolder, "Videos");
        public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");
    }

    private sealed class TestClock : IClock
    {
        public DateTime Now => UtcNow.ToLocalTime();
        public DateTime UtcNow => CaptureAssetBootstrapperTests.UtcNow;
    }

    private sealed class TestChangeSignal : ICaptureAssetChangeSignal
    {
        public int SignalCount { get; private set; }

        public bool TrySignal()
        {
            SignalCount++;
            return true;
        }
    }

    private sealed class TestLogService : ILogService
    {
        public event EventHandler<ILogEntry>? LogAdded
        {
            add { }
            remove { }
        }

        public bool IsEnabled => true;
        public void ClearLogs() { }
        public void Disable() { }
        public void Enable() { }
        public IEnumerable<ILogEntry> GetLogs() => [];
        public void LogException(Exception e, string? message = null) { }
        public void LogInformation(string info) { }
        public void LogWarning(string warning) { }
    }

    public TestContext TestContext { get; set; } = null!;
}
