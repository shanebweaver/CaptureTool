using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.CaptureAssets.Serialization;
using System.Text;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Tests.CaptureAssets;

[TestClass]
public sealed class LocalCaptureAssetCatalogTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AddAndUpdate_ShouldPersistProtectedAssetAndOrderedContentFreeChanges()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);
        CaptureAsset asset = CreateAsset(Path.Combine(dataFolder, "Captures", "retained.png"));

        var added = catalog.TryAdd(asset);
        CaptureAsset updated = asset.ChangePreferredOpenPath(
            Path.Combine(dataFolder, "Pictures", "capture.png"));
        var changed = catalog.TryUpdate(
            updated,
            asset.LifecycleRevision,
            CaptureAssetChangeType.PreferredLocationChanged);

        Assert.IsTrue(added.Succeeded);
        Assert.IsTrue(added.Changed);
        Assert.AreEqual(1L, added.ChangeSequence);
        Assert.IsTrue(changed.Succeeded);
        Assert.IsTrue(changed.Changed);
        Assert.AreEqual(2L, changed.ChangeSequence);

        string catalogPath = GetCatalogPath(dataFolder);
        byte[] protectedBytes = File.ReadAllBytes(catalogPath);
        string protectedText = Encoding.UTF8.GetString(protectedBytes);
        Assert.DoesNotContain(asset.RetainedSourcePath, protectedText);
        Assert.DoesNotContain(updated.PreferredOpenPath!, protectedText);

        LocalCaptureAssetCatalog reloaded = CreateCatalog(dataFolder, protector);
        CaptureAsset? persisted = reloaded.Get(asset.Id);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(asset.Id, persisted.Id);
        Assert.AreEqual(asset.RetainedSourcePath, persisted.RetainedSourcePath);
        Assert.AreEqual(updated.PreferredOpenPath, persisted.PreferredOpenPath);
        Assert.AreEqual(2L, persisted.LifecycleRevision);

        IReadOnlyList<CaptureAssetChange> changes = reloaded.GetChangesAfter(0);
        Assert.HasCount(2, changes);
        Assert.AreEqual(CaptureAssetChangeType.Finalized, changes[0].ChangeType);
        Assert.AreEqual(CaptureAssetChangeType.PreferredLocationChanged, changes[1].ChangeType);
        Assert.IsTrue(changes.Zip(changes.Skip(1)).All(pair => pair.First.Sequence < pair.Second.Sequence));
        Assert.IsFalse(typeof(CaptureAssetChange).GetProperties().Any(property =>
            property.PropertyType == typeof(string)));
    }

    [TestMethod]
    public void TryForget_ShouldRemoveAllActivePathsAndPersistOnlyContentFreeFacts()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);
        CaptureAsset asset = CreateAsset(Path.Combine(dataFolder, "Captures", "retained.png"));
        Assert.IsTrue(catalog.TryAdd(asset).Succeeded);

        CaptureAssetCatalogWriteResult forgotten = catalog.TryForget(
            asset.Id,
            asset.LifecycleRevision);

        Assert.IsTrue(forgotten.Succeeded);
        Assert.IsTrue(forgotten.Changed);
        Assert.AreEqual(2L, forgotten.ChangeSequence);
        Assert.IsNull(forgotten.Asset);
        Assert.IsNull(catalog.Get(asset.Id));
        Assert.IsNull(catalog.FindByPath(asset.RetainedSourcePath));
        Assert.IsEmpty(catalog.GetAssets());
        Assert.AreEqual(
            CaptureAssetChangeType.Forgotten,
            catalog.GetChangesAfter(0)[^1].ChangeType);

        LocalCaptureAssetCatalog reloaded = CreateCatalog(dataFolder, protector);
        Assert.IsNull(reloaded.Get(asset.Id));
        Assert.IsEmpty(reloaded.GetAssets());
        Assert.HasCount(2, reloaded.GetChangesAfter(0));
        Assert.AreEqual(
            CaptureAssetChangeType.Forgotten,
            reloaded.GetChangesAfter(0)[^1].ChangeType);

        CaptureAssetCatalogWriteResult retry = reloaded.TryForget(
            asset.Id,
            asset.LifecycleRevision);
        Assert.IsTrue(retry.Succeeded);
        Assert.IsFalse(retry.Changed);
        Assert.AreEqual(2L, retry.ChangeSequence);
    }

    [TestMethod]
    public void TryAddRange_ShouldCoalescePathsAndRemainIdempotent()
    {
        string dataFolder = CreateTestFolder();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);
        string retainedPath = Path.Combine(dataFolder, "Captures", "retained.png");
        CaptureAsset first = CreateAsset(retainedPath);
        var duplicate = new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            retainedPath.ToUpperInvariant(),
            CaptureSourceOwnership.LegacyExternal,
            CapturedAtUtc,
            preferredOpenPath: retainedPath);

        IReadOnlyList<CaptureAssetCatalogWriteResult> results =
            catalog.TryAddRange([first, duplicate]);

        Assert.HasCount(2, results);
        Assert.IsTrue(results[0].Changed);
        Assert.IsFalse(results[1].Changed);
        Assert.AreEqual(first.Id, results[1].Asset?.Id);
        Assert.AreEqual(1L, results[1].ChangeSequence);
        Assert.HasCount(1, catalog.GetAssets());
        Assert.HasCount(1, catalog.GetChangesAfter(0));

        var retried = catalog.TryAdd(duplicate);
        Assert.IsTrue(retried.Succeeded);
        Assert.IsFalse(retried.Changed);
        Assert.AreEqual(first.Id, retried.Asset?.Id);
        Assert.AreEqual(1L, retried.ChangeSequence);
    }

    [TestMethod]
    public void MigratedLegacyAsset_ShouldPersistSameSourceAndPreferredPath()
    {
        string dataFolder = CreateTestFolder();
        string legacyPath = Path.Combine(dataFolder, "Legacy", "capture.png");
        var legacyAsset = new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            legacyPath,
            CaptureSourceOwnership.LegacyExternal,
            CapturedAtUtc,
            preferredOpenPath: legacyPath);
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);

        Assert.IsTrue(catalog.TryAdd(legacyAsset).Succeeded);
        CaptureAsset? reloaded = CreateCatalog(dataFolder).Get(legacyAsset.Id);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(Path.GetFullPath(legacyPath), reloaded.RetainedSourcePath);
        Assert.AreEqual(reloaded.RetainedSourcePath, reloaded.PreferredOpenPath);
        Assert.AreEqual(CaptureSourceOwnership.LegacyExternal, reloaded.SourceOwnership);
    }

    [TestMethod]
    public void FailedProtectedWrite_ShouldNotCommitCandidateState()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService { FailProtect = true };
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);
        CaptureAsset asset = CreateAsset(Path.Combine(dataFolder, "Captures", "retained.png"));

        var result = catalog.TryAdd(asset);

        Assert.IsFalse(result.Succeeded);
        Assert.IsEmpty(catalog.GetAssets());
        Assert.IsEmpty(catalog.GetChangesAfter(0));
        Assert.IsFalse(File.Exists(GetCatalogPath(dataFolder)));
    }

    [TestMethod]
    public void CorruptCatalog_ShouldRemainUntouchedAndUnavailableForWrites()
    {
        string dataFolder = CreateTestFolder();
        string catalogPath = GetCatalogPath(dataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        byte[] corruptBytes = [1, 2, 3, 4, 5];
        File.WriteAllBytes(catalogPath, corruptBytes);
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);

        Assert.IsEmpty(catalog.GetAssets());
        var result = catalog.TryAdd(CreateAsset(Path.Combine(dataFolder, "Captures", "retained.png")));

        Assert.IsFalse(result.Succeeded);
        CollectionAssert.AreEqual(corruptBytes, File.ReadAllBytes(catalogPath));
    }

    [TestMethod]
    public void CatalogWithRepeatedFinalizedChange_ShouldRemainUnavailable()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAsset asset = new(
            CaptureId.New(),
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "retained.png"),
            CaptureSourceOwnership.AppOwned,
            preferredOpenPath: null,
            capturedAtUtc: CapturedAtUtc,
            lifecycleState: CaptureAssetLifecycleState.Active,
            lifecycleRevision: 2);
        WriteCatalog(
            dataFolder,
            protector,
            asset,
            (1, CaptureAssetChangeType.Finalized),
            (2, CaptureAssetChangeType.Finalized));
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);

        Assert.IsEmpty(catalog.GetAssets());
        Assert.IsFalse(catalog.TryAdd(CreateAsset(
            Path.Combine(dataFolder, "Captures", "other.png"))).Succeeded);
    }

    [TestMethod]
    public void CatalogWithNonTerminalDeletedChange_ShouldRemainUnavailable()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAsset asset = new(
            CaptureId.New(),
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "retained.png"),
            CaptureSourceOwnership.AppOwned,
            preferredOpenPath: null,
            capturedAtUtc: CapturedAtUtc,
            lifecycleState: CaptureAssetLifecycleState.Deleted,
            lifecycleRevision: 4);
        WriteCatalog(
            dataFolder,
            protector,
            asset,
            (1, CaptureAssetChangeType.Finalized),
            (2, CaptureAssetChangeType.Deleted),
            (3, CaptureAssetChangeType.PreferredLocationChanged),
            (4, CaptureAssetChangeType.Deleted));
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);

        Assert.IsEmpty(catalog.GetAssets());
        Assert.IsFalse(catalog.TryAdd(CreateAsset(
            Path.Combine(dataFolder, "Captures", "other.png"))).Succeeded);
    }

    [TestMethod]
    public void CatalogWithChangeSequenceGap_ShouldRemainUnavailable()
    {
        string dataFolder = CreateTestFolder();
        var protector = new TestDataProtectionService();
        CaptureAsset asset = new(
            CaptureId.New(),
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "retained.png"),
            CaptureSourceOwnership.AppOwned,
            preferredOpenPath: Path.Combine(dataFolder, "Pictures", "capture.png"),
            capturedAtUtc: CapturedAtUtc,
            lifecycleState: CaptureAssetLifecycleState.Active,
            lifecycleRevision: 2);
        WriteCatalog(
            dataFolder,
            protector,
            asset,
            (1, CaptureAssetChangeType.Finalized),
            (3, CaptureAssetChangeType.PreferredLocationChanged));
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder, protector);

        Assert.IsEmpty(catalog.GetAssets());
        Assert.IsFalse(catalog.TryAdd(CreateAsset(
            Path.Combine(dataFolder, "Captures", "other.png"))).Succeeded);
    }

    [TestMethod]
    public void TryUpdate_ShouldRejectStaleRevisionWithoutAppendingChange()
    {
        string dataFolder = CreateTestFolder();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);
        CaptureAsset asset = CreateAsset(Path.Combine(dataFolder, "Captures", "retained.png"));
        Assert.IsTrue(catalog.TryAdd(asset).Succeeded);
        CaptureAsset firstUpdate = asset.ChangePreferredOpenPath(
            Path.Combine(dataFolder, "Pictures", "first.png"));
        Assert.IsTrue(catalog.TryUpdate(
            firstUpdate,
            asset.LifecycleRevision,
            CaptureAssetChangeType.PreferredLocationChanged).Succeeded);
        CaptureAsset staleUpdate = asset.ChangePreferredOpenPath(
            Path.Combine(dataFolder, "Pictures", "stale.png"));

        var result = catalog.TryUpdate(
            staleUpdate,
            asset.LifecycleRevision,
            CaptureAssetChangeType.PreferredLocationChanged);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(firstUpdate.PreferredOpenPath, catalog.Get(asset.Id)?.PreferredOpenPath);
        Assert.HasCount(2, catalog.GetChangesAfter(0));
    }

    [TestMethod]
    public void TryAdd_ShouldNormalizeAliasedPathsAndEnforcePreferredPathUniqueness()
    {
        string dataFolder = CreateTestFolder();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);
        string canonicalPath = Path.Combine(dataFolder, "Captures", "capture.png");
        string aliasedPath = Path.Combine(dataFolder, "Captures", "nested", "..", "capture.png");
        string preferredPath = Path.Combine(dataFolder, "Pictures", "capture.png");
        var first = new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            aliasedPath,
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc,
            preferredPath);
        Assert.IsTrue(catalog.TryAdd(first).Succeeded);

        CaptureAsset sameSource = CreateAsset(canonicalPath);
        var samePreferred = new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "other.png"),
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc,
            preferredPath);
        var sourceResult = catalog.TryAdd(sameSource);
        var preferredResult = catalog.TryAdd(samePreferred);

        Assert.AreEqual(Path.GetFullPath(canonicalPath), first.RetainedSourcePath);
        Assert.AreEqual(first.Id, sourceResult.Asset?.Id);
        Assert.AreEqual(first.Id, preferredResult.Asset?.Id);
        Assert.HasCount(1, catalog.GetAssets());
    }

    [TestMethod]
    public void TryUpdate_ShouldRejectPathCollisionAndInvalidFinalizedState()
    {
        string dataFolder = CreateTestFolder();
        LocalCaptureAssetCatalog catalog = CreateCatalog(dataFolder);
        CaptureAsset first = CreateAsset(Path.Combine(dataFolder, "Captures", "first.png"));
        CaptureAsset second = CreateAsset(Path.Combine(dataFolder, "Captures", "second.png"));
        Assert.IsTrue(catalog.TryAdd(first).Succeeded);
        Assert.IsTrue(catalog.TryAdd(second).Succeeded);

        CaptureAsset colliding = second.ChangePreferredOpenPath(first.RetainedSourcePath);
        var updateResult = catalog.TryUpdate(
            colliding,
            second.LifecycleRevision,
            CaptureAssetChangeType.PreferredLocationChanged);
        CaptureAsset invalidFinalized = new(
            CaptureId.New(),
            CaptureFileType.Image,
            Path.Combine(dataFolder, "Captures", "deleted.png"),
            CaptureSourceOwnership.AppOwned,
            preferredOpenPath: null,
            capturedAtUtc: CapturedAtUtc,
            lifecycleState: CaptureAssetLifecycleState.Deleted,
            lifecycleRevision: 2);
        var addResult = catalog.TryAdd(invalidFinalized);

        Assert.IsFalse(updateResult.Succeeded);
        Assert.IsFalse(addResult.Succeeded);
        Assert.HasCount(2, catalog.GetAssets());
        Assert.HasCount(2, catalog.GetChangesAfter(0));
    }

    private static CaptureAsset CreateAsset(string retainedPath)
    {
        return new(
            CaptureId.New(),
            CaptureFileType.Image,
            retainedPath,
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);
    }

    private static void WriteCatalog(
        string dataFolder,
        TestDataProtectionService protector,
        CaptureAsset asset,
        params (long Sequence, CaptureAssetChangeType ChangeType)[] changes)
    {
        var document = new CaptureAssetCatalogDocument
        {
            SchemaVersion = 1,
            LastSequence = changes[^1].Sequence,
            Assets =
            [
                new CaptureAssetDocument
                {
                    CaptureId = asset.Id.ToString(),
                    MediaType = asset.MediaType,
                    RetainedSourcePath = asset.RetainedSourcePath,
                    SourceOwnership = asset.SourceOwnership,
                    PreferredOpenPath = asset.PreferredOpenPath,
                    CapturedAtUtc = asset.CapturedAtUtc,
                    LifecycleState = asset.LifecycleState,
                    LifecycleRevision = asset.LifecycleRevision,
                },
            ],
            Changes = changes
                .Select((change, index) => new CaptureAssetChangeDocument
                {
                    Sequence = change.Sequence,
                    CaptureId = asset.Id.ToString(),
                    LifecycleRevision = index + 1,
                    ChangeType = change.ChangeType,
                    ChangedAtUtc = CapturedAtUtc.AddMinutes(index),
                })
                .ToList(),
        };
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            document,
            CaptureAssetCatalogContext.Default.CaptureAssetCatalogDocument);
        string catalogPath = GetCatalogPath(dataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllBytes(catalogPath, protector.Protect(plaintext));
    }

    private static LocalCaptureAssetCatalog CreateCatalog(
        string dataFolder,
        TestDataProtectionService? protector = null)
    {
        return new(
            new TestStorageService(dataFolder),
            protector ?? new TestDataProtectionService(),
            new TestClock(),
            new TestLogService());
    }

    private static string GetCatalogPath(string dataFolder)
    {
        return Path.Combine(
            dataFolder,
            LocalCaptureAssetCatalog.CatalogDirectoryName,
            LocalCaptureAssetCatalog.CatalogVersionDirectoryName,
            LocalCaptureAssetCatalog.CatalogFileName);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestDataProtectionService : IUserDataProtectionService
    {
        private const byte Marker = 0xA5;

        public bool FailProtect { get; init; }

        public byte[] Protect(byte[] plaintext)
        {
            if (FailProtect)
            {
                throw new IOException("Protection unavailable.");
            }

            byte[] result = new byte[plaintext.Length + 1];
            result[0] = Marker;
            for (int index = 0; index < plaintext.Length; index++)
            {
                result[index + 1] = (byte)(plaintext[index] ^ Marker);
            }

            return result;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            if (protectedData.Length == 0 || protectedData[0] != Marker)
            {
                throw new InvalidDataException("Invalid protected payload.");
            }

            byte[] result = new byte[protectedData.Length - 1];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = (byte)(protectedData[index + 1] ^ Marker);
            }

            return result;
        }
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
        public DateTime UtcNow => CapturedAtUtc.UtcDateTime.AddMinutes(1);
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
}
