using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.RecentCaptures;
using CaptureTool.Infrastructure.RecentCaptures.Serialization;

namespace CaptureTool.Infrastructure.Tests.RecentCaptures;

[TestClass]
public sealed class LocalRecentCaptureCatalogTests
{
    [TestMethod]
    public void RecordCapturedAndOpened_ShouldPersistAcrossInstances()
    {
        string dataFolder = CreateTestFolder();
        DateTime activityUtc = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        string capturedPath = Path.Combine(dataFolder, "captured.png");
        string openedPath = Path.Combine(dataFolder, "opened.mp4");
        CaptureId captureId = CaptureId.New();

        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder, () => activityUtc);
        catalog.RecordCaptured(capturedPath, CaptureFileType.Image, captureId);
        activityUtc = activityUtc.AddMinutes(1);
        catalog.RecordOpened(openedPath, CaptureFileType.Video);

        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder, () => activityUtc);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = reloadedCatalog.GetEntries();

        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.Any(entry =>
            entry.FilePath == Path.GetFullPath(capturedPath) &&
            entry.Origin == RecentCaptureOrigin.Captured &&
            entry.CaptureFileType == CaptureFileType.Image &&
            entry.CaptureId == captureId));
        Assert.IsTrue(entries.Any(entry =>
            entry.FilePath == Path.GetFullPath(openedPath) &&
            entry.Origin == RecentCaptureOrigin.Opened &&
            entry.CaptureFileType == CaptureFileType.Video &&
            entry.CaptureId is null));
    }

    [TestMethod]
    public void LegacyCatalogWithoutCaptureIds_ShouldLoadAndRemainCompatible()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "legacy-capture.png");
        string escapedPath = capturedPath.Replace("\\", "\\\\", StringComparison.Ordinal);
        string legacyJson = $$"""
            [
              {
                "FilePath": "{{escapedPath}}",
                "CaptureFileType": 0,
                "Origin": 0,
                "LastActivityUtc": "2026-07-31T12:00:00Z"
              }
            ]
            """;
        File.WriteAllText(Path.Combine(dataFolder, "RecentCaptures.json"), legacyJson);

        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        RecentCaptureCatalogEntry entry = entries[0];
        Assert.AreEqual(Path.GetFullPath(capturedPath), entry.FilePath);
        Assert.AreEqual(RecentCaptureOrigin.Captured, entry.Origin);
        Assert.IsNull(entry.CaptureId);
        Assert.AreEqual(0, catalog.GetCaptureAssetChangeCheckpoint());

        catalog.Touch(capturedPath);
        StringAssert.StartsWith(
            File.ReadAllText(GetCatalogFilePath(dataFolder)).TrimStart(),
            "{");
        IReadOnlyList<RecentCaptureCatalogEntry> reloadedEntries = CreateCatalog(dataFolder).GetEntries();
        Assert.HasCount(1, reloadedEntries);
        RecentCaptureCatalogEntry reloadedEntry = reloadedEntries[0];
        Assert.IsNull(reloadedEntry.CaptureId);

        CaptureId migratedCaptureId = CaptureId.New();
        Assert.IsTrue(catalog.TryAssignCaptureId(capturedPath, migratedCaptureId));
        Assert.IsFalse(catalog.TryAssignCaptureId(capturedPath, migratedCaptureId));

        IReadOnlyList<RecentCaptureCatalogEntry> migratedEntries = CreateCatalog(dataFolder).GetEntries();
        Assert.HasCount(1, migratedEntries);
        Assert.AreEqual(migratedCaptureId, migratedEntries[0].CaptureId);
    }

    [TestMethod]
    public void NewEnvelope_ShouldLoadAndAdvanceAcrossPersistedOutOfOrderSequences()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "captured.png");
        string escapedPath = capturedPath.Replace("\\", "\\\\", StringComparison.Ordinal);
        CaptureId captureId = CaptureId.New();
        string envelopeJson = $$"""
            {
              "schemaVersion": 1,
              "assetChangeCheckpoint": 2,
              "appliedOutOfOrderSequences": [4, 6],
              "entries": [
                {
                  "FilePath": "{{escapedPath}}",
                  "CaptureFileType": 0,
                  "Origin": 0,
                  "LastActivityUtc": "2026-07-31T12:00:00Z",
                  "CaptureId": "{{captureId}}"
                }
              ]
            }
            """;
        File.WriteAllText(GetCatalogFilePath(dataFolder), envelopeJson);
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        Assert.AreEqual(2, catalog.GetCaptureAssetChangeCheckpoint());
        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.AreEqual(captureId, entries[0].CaptureId);

        Assert.IsTrue(catalog.TryAdvanceCaptureAssetChangeCheckpoint(3));
        Assert.AreEqual(4, catalog.GetCaptureAssetChangeCheckpoint());
        Assert.IsTrue(catalog.TryAdvanceCaptureAssetChangeCheckpoint(5));
        Assert.AreEqual(6, catalog.GetCaptureAssetChangeCheckpoint());

        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder);
        Assert.AreEqual(6, reloadedCatalog.GetCaptureAssetChangeCheckpoint());
        Assert.AreEqual(captureId, reloadedCatalog.GetEntries()[0].CaptureId);
    }

    [TestMethod]
    public void CorruptCatalog_ShouldDurablyDisableRetainedCaptureRecovery()
    {
        string dataFolder = CreateTestFolder();
        string retainedPath = Path.Combine(dataFolder, "Captures", "unproven.png");
        File.WriteAllText(GetCatalogFilePath(dataFolder), "{ not valid json");
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        Assert.IsEmpty(catalog.GetEntries());
        Assert.IsTrue(catalog.IsRetainedCaptureRecoveryExcluded(retainedPath));

        catalog.RecordOpened(Path.Combine(dataFolder, "opened.png"), CaptureFileType.Image);

        LocalRecentCaptureCatalog reloaded = CreateCatalog(dataFolder);
        Assert.IsTrue(reloaded.IsRetainedCaptureRecoveryExcluded(retainedPath));
    }

    [TestMethod]
    public void OutOfOrderAcknowledgements_ShouldAdvanceOnlyAcrossContiguousSequences()
    {
        string dataFolder = CreateTestFolder();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        Assert.IsTrue(catalog.TryAdvanceCaptureAssetChangeCheckpoint(4));
        Assert.IsTrue(catalog.TryAdvanceCaptureAssetChangeCheckpoint(2));
        Assert.AreEqual(0, catalog.GetCaptureAssetChangeCheckpoint());
        StringAssert.Contains(
            File.ReadAllText(GetCatalogFilePath(dataFolder)),
            "\"appliedOutOfOrderSequences\":[2,4]");

        bool[] acknowledgementResults = new bool[2];
        Parallel.Invoke(
            () => acknowledgementResults[0] = catalog.TryAdvanceCaptureAssetChangeCheckpoint(1),
            () => acknowledgementResults[1] = catalog.TryAdvanceCaptureAssetChangeCheckpoint(3));

        Assert.IsTrue(acknowledgementResults.All(result => result));
        Assert.AreEqual(4, catalog.GetCaptureAssetChangeCheckpoint());
        Assert.IsTrue(catalog.TryAdvanceCaptureAssetChangeCheckpoint(4));

        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder);
        Assert.AreEqual(4, reloadedCatalog.GetCaptureAssetChangeCheckpoint());

        Assert.IsTrue(reloadedCatalog.TryAdvanceCaptureAssetChangeCheckpoint(6));
        reloadedCatalog.Clear(throughChangeSequence: 5);
        Assert.AreEqual(6, reloadedCatalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public void TryProjectCaptured_ShouldPersistEntryAndSequenceWithSuppliedActivity()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "captured.png");
        DateTime activityUtc = new(2026, 7, 31, 12, 34, 56, DateTimeKind.Utc);
        CaptureId captureId = CaptureId.New();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        bool projected = catalog.TryProjectCaptured(
            capturedPath,
            CaptureFileType.Image,
            captureId,
            changeSequence: 1,
            activityUtc);

        Assert.IsTrue(projected);
        Assert.AreEqual(1, catalog.GetCaptureAssetChangeCheckpoint());
        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = reloadedCatalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.AreEqual(Path.GetFullPath(capturedPath), entries[0].FilePath);
        Assert.AreEqual(CaptureFileType.Image, entries[0].CaptureFileType);
        Assert.AreEqual(RecentCaptureOrigin.Captured, entries[0].Origin);
        Assert.AreEqual(captureId, entries[0].CaptureId);
        Assert.AreEqual(activityUtc, entries[0].LastActivityUtc);
        Assert.AreEqual(1, reloadedCatalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public void AppliedProjection_ShouldRepairExistingIdentityAndCoalesceTargetPath()
    {
        string dataFolder = CreateTestFolder();
        string temporaryPath = Path.Combine(dataFolder, "Temp", "capture.png");
        string savedPath = Path.Combine(dataFolder, "Pictures", "capture.png");
        DateTime capturedUtc = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        DateTime savedUtc = capturedUtc.AddMinutes(1);
        CaptureId captureId = CaptureId.New();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        Assert.IsTrue(catalog.TryProjectCaptured(
            temporaryPath,
            CaptureFileType.Video,
            captureId,
            changeSequence: 1,
            capturedUtc));
        catalog.RecordOpened(savedPath, CaptureFileType.Image);

        bool repaired = catalog.TryProjectCaptured(
            savedPath,
            CaptureFileType.Image,
            captureId,
            changeSequence: 1,
            savedUtc);

        Assert.IsTrue(repaired);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.AreEqual(Path.GetFullPath(savedPath), entries[0].FilePath);
        Assert.AreEqual(CaptureFileType.Image, entries[0].CaptureFileType);
        Assert.AreEqual(captureId, entries[0].CaptureId);
        Assert.AreEqual(savedUtc, entries[0].LastActivityUtc);
        Assert.AreEqual(1, catalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public async Task ClearThroughSequence_ShouldNotDeleteMediaOrReplayCheckpointedCapture()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "captured.png");
        await File.WriteAllTextAsync(capturedPath, "capture", TestContext.CancellationToken);
        DateTime activityUtc = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        CaptureId captureId = CaptureId.New();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        Assert.IsTrue(catalog.TryProjectCaptured(
            capturedPath,
            CaptureFileType.Image,
            captureId,
            changeSequence: 1,
            activityUtc));

        catalog.Clear(throughChangeSequence: 1);

        Assert.IsEmpty(catalog.GetEntries());
        Assert.AreEqual(1, catalog.GetCaptureAssetChangeCheckpoint());
        Assert.IsTrue(File.Exists(capturedPath));
        Assert.IsTrue(catalog.TryProjectCaptured(
            capturedPath,
            CaptureFileType.Image,
            captureId,
            changeSequence: 1,
            activityUtc));
        Assert.IsEmpty(catalog.GetEntries());

        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder);
        Assert.IsEmpty(reloadedCatalog.GetEntries());
        Assert.AreEqual(1, reloadedCatalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public void ClearThroughSequence_ShouldRespectProjectionLockOrdering()
    {
        string dataFolder = CreateTestFolder();
        string beforeClearPath = Path.Combine(dataFolder, "before-clear.png");
        string afterClearPath = Path.Combine(dataFolder, "after-clear.png");
        DateTime activityUtc = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        CaptureId beforeClearId = CaptureId.New();
        CaptureId afterClearId = CaptureId.New();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        Assert.IsTrue(catalog.TryProjectCaptured(
            beforeClearPath,
            CaptureFileType.Image,
            beforeClearId,
            changeSequence: 2,
            activityUtc));
        catalog.Clear(throughChangeSequence: 1);

        Assert.AreEqual(2, catalog.GetCaptureAssetChangeCheckpoint());
        Assert.IsEmpty(catalog.GetEntries());
        Assert.IsTrue(catalog.TryProjectCaptured(
            beforeClearPath,
            CaptureFileType.Image,
            beforeClearId,
            changeSequence: 2,
            activityUtc));
        Assert.IsEmpty(catalog.GetEntries());

        Assert.IsTrue(catalog.TryProjectCaptured(
            afterClearPath,
            CaptureFileType.Image,
            afterClearId,
            changeSequence: 3,
            activityUtc.AddSeconds(1)));
        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.AreEqual(afterClearId, entries[0].CaptureId);
        Assert.AreEqual(3, catalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public void ProjectionSaveFailure_ShouldNotCommitCandidateStateInMemory()
    {
        string dataFolder = CreateTestFolder();
        Directory.CreateDirectory(GetCatalogFilePath(dataFolder));
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        bool projected = catalog.TryProjectCaptured(
            Path.Combine(dataFolder, "captured.png"),
            CaptureFileType.Image,
            CaptureId.New(),
            changeSequence: 1,
            new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        bool advanced = catalog.TryAdvanceCaptureAssetChangeCheckpoint(1);
        catalog.Clear(throughChangeSequence: 1);

        Assert.IsFalse(projected);
        Assert.IsFalse(advanced);
        Assert.IsEmpty(catalog.GetEntries());
        Assert.AreEqual(0, catalog.GetCaptureAssetChangeCheckpoint());
    }

    [TestMethod]
    public void ReplacePath_ShouldTrackAutoSavedFileInsteadOfTemporaryFile()
    {
        string dataFolder = CreateTestFolder();
        string temporaryPath = Path.Combine(dataFolder, "Temp", "capture.wav");
        string savedPath = Path.Combine(dataFolder, "Music", "capture.wav");
        CaptureId captureId = CaptureId.New();
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        catalog.RecordCaptured(temporaryPath, CaptureFileType.Audio, captureId);
        catalog.ReplacePath(temporaryPath, savedPath);

        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        RecentCaptureCatalogEntry entry = entries[0];
        Assert.AreEqual(Path.GetFullPath(savedPath), entry.FilePath);
        Assert.AreEqual(RecentCaptureOrigin.Captured, entry.Origin);
        Assert.AreEqual(CaptureFileType.Audio, entry.CaptureFileType);
        Assert.AreEqual(captureId, entry.CaptureId);
    }

    [TestMethod]
    public void RecordOpened_ShouldNotAddOrReplaceCaptureIdentity()
    {
        string dataFolder = CreateTestFolder();
        string openedPath = Path.Combine(dataFolder, "opened.png");
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        catalog.RecordOpened(openedPath, CaptureFileType.Image);
        catalog.RecordOpened(openedPath, CaptureFileType.Image);

        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        RecentCaptureCatalogEntry entry = entries[0];
        Assert.AreEqual(RecentCaptureOrigin.Opened, entry.Origin);
        Assert.IsNull(entry.CaptureId);
    }

    [TestMethod]
    public void TryAssignCaptureId_ShouldRepairCapturedEntryWithoutChangingActivity()
    {
        string dataFolder = CreateTestFolder();
        string capturedPath = Path.Combine(dataFolder, "captured.png");
        DateTime activityUtc = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        CaptureId captureId = CaptureId.New();
        TestRecentCapturesChangeNotifier changeNotifier = new();
        LocalRecentCaptureCatalog catalog = CreateCatalog(
            dataFolder,
            () => activityUtc,
            changeNotifier);
        catalog.RecordCaptured(capturedPath, CaptureFileType.Image);
        DateTime originalActivityUtc = catalog.GetEntries()[0].LastActivityUtc;
        activityUtc = activityUtc.AddHours(1);

        bool assigned = catalog.TryAssignCaptureId(capturedPath, captureId);
        bool assignedAgain = catalog.TryAssignCaptureId(capturedPath, captureId);

        Assert.IsTrue(assigned);
        Assert.IsFalse(assignedAgain);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.AreEqual(captureId, entries[0].CaptureId);
        Assert.AreEqual(originalActivityUtc, entries[0].LastActivityUtc);
        Assert.AreEqual(2, changeNotifier.NotificationCount);
    }

    [TestMethod]
    public void TryAssignCaptureId_ShouldIgnoreOpenedEntry()
    {
        string dataFolder = CreateTestFolder();
        string openedPath = Path.Combine(dataFolder, "opened.png");
        TestRecentCapturesChangeNotifier changeNotifier = new();
        LocalRecentCaptureCatalog catalog = CreateCatalog(
            dataFolder,
            changeNotifier: changeNotifier);
        catalog.RecordOpened(openedPath, CaptureFileType.Image);
        DateTime originalActivityUtc = catalog.GetEntries()[0].LastActivityUtc;

        bool assigned = catalog.TryAssignCaptureId(openedPath, CaptureId.New());

        Assert.IsFalse(assigned);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        Assert.IsNull(entries[0].CaptureId);
        Assert.AreEqual(originalActivityUtc, entries[0].LastActivityUtc);
        Assert.AreEqual(1, changeNotifier.NotificationCount);
    }

    [TestMethod]
    public void Clear_ShouldPersistRetainedOrphanRecoveryExclusions()
    {
        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        string openedPath = Path.Combine(retainedFolder, "opened.png");
        string unidentifiedCapturePath = Path.Combine(retainedFolder, "captured.png");
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        catalog.RecordOpened(openedPath, CaptureFileType.Image);
        catalog.RecordCaptured(unidentifiedCapturePath, CaptureFileType.Image);

        catalog.Clear();

        Assert.IsTrue(catalog.IsRetainedCaptureRecoveryExcluded(openedPath));
        Assert.IsTrue(catalog.IsRetainedCaptureRecoveryExcluded(unidentifiedCapturePath));
        string persistedJson = File.ReadAllText(GetCatalogFilePath(dataFolder));
        Assert.DoesNotContain(Path.GetFileName(openedPath), persistedJson);
        Assert.DoesNotContain(Path.GetFileName(unidentifiedCapturePath), persistedJson);
        LocalRecentCaptureCatalog reloaded = CreateCatalog(dataFolder);
        Assert.IsTrue(reloaded.IsRetainedCaptureRecoveryExcluded(openedPath));
        Assert.IsTrue(reloaded.IsRetainedCaptureRecoveryExcluded(unidentifiedCapturePath));
    }

    [TestMethod]
    public void RecoveryExclusionsBeyondBound_ShouldFailClosedWithoutPersistingPaths()
    {
        string dataFolder = CreateTestFolder();
        string retainedFolder = Path.Combine(dataFolder, "Captures");
        List<RecentCaptureCatalogEntry> legacyEntries = Enumerable.Range(0, 1000)
            .Select(index => new RecentCaptureCatalogEntry(
                Path.Combine(retainedFolder, $"opened-{index:D4}.png"),
                CaptureFileType.Image,
                RecentCaptureOrigin.Opened,
                new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc).AddSeconds(index)))
            .ToList();
        File.WriteAllText(
            GetCatalogFilePath(dataFolder),
            System.Text.Json.JsonSerializer.Serialize(
                legacyEntries,
                RecentCaptureCatalogContext.Default.ListRecentCaptureCatalogEntry));
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        string extraPath = Path.Combine(retainedFolder, "extra.png");

        catalog.Clear();
        catalog.RecordOpened(extraPath, CaptureFileType.Image);
        Assert.IsTrue(catalog.Remove(extraPath));

        string persistedJson = File.ReadAllText(GetCatalogFilePath(dataFolder));
        Assert.DoesNotContain("opened-0000.png", persistedJson);
        Assert.DoesNotContain("extra.png", persistedJson);
        StringAssert.Contains(persistedJson, "\"retainedCaptureRecoveryDisabled\":true");
        LocalRecentCaptureCatalog reloaded = CreateCatalog(dataFolder);
        Assert.IsTrue(reloaded.IsRetainedCaptureRecoveryExcluded(
            Path.Combine(retainedFolder, "any-unproven-file.png")));
    }

    [TestMethod]
    public void Clear_WhenRetainedFolderLookupFails_ShouldDurablyDisableRecovery()
    {
        string dataFolder = CreateTestFolder();
        string retainedPath = Path.Combine(dataFolder, "Captures", "captured.png");
        LocalRecentCaptureCatalog catalog = CreateCatalog(
            dataFolder,
            storageService: new ThrowingRetainedFolderStorageService(dataFolder));
        catalog.RecordCaptured(retainedPath, CaptureFileType.Image);

        catalog.Clear();

        Assert.IsEmpty(catalog.GetEntries());
        LocalRecentCaptureCatalog reloaded = CreateCatalog(dataFolder);
        Assert.IsTrue(reloaded.IsRetainedCaptureRecoveryExcluded(retainedPath));
        Assert.DoesNotContain(
            Path.GetFileName(retainedPath),
            File.ReadAllText(GetCatalogFilePath(dataFolder)));
    }

    [TestMethod]
    public void TryRepairCapturedProjection_ShouldAtomicallyPreserveKnownIdentity()
    {
        string dataFolder = CreateTestFolder();
        string retainedPath = Path.Combine(dataFolder, "Captures", "retained.png");
        string preferredPath = Path.Combine(dataFolder, "Pictures", "capture.png");
        CaptureId captureId = CaptureId.New();
        DateTime activityUtc = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        catalog.RecordCaptured(retainedPath, CaptureFileType.Image);

        bool repaired = catalog.TryRepairCapturedProjection(
            retainedPath,
            preferredPath,
            CaptureFileType.Image,
            captureId,
            activityUtc);

        Assert.IsTrue(repaired);
        RecentCaptureCatalogEntry entry = catalog.GetEntries().Single();
        Assert.AreEqual(Path.GetFullPath(preferredPath), entry.FilePath);
        Assert.AreEqual(captureId, entry.CaptureId);
        Assert.AreEqual(activityUtc, entry.LastActivityUtc);
    }

    [TestMethod]
    public void TryAssignCaptureId_WhenSaveFails_ShouldNotCommitCandidateInMemory()
    {
        string dataFolder = CreateTestFolder();
        string retainedPath = Path.Combine(dataFolder, "Captures", "capture.png");
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        catalog.RecordCaptured(retainedPath, CaptureFileType.Image);
        string catalogPath = GetCatalogFilePath(dataFolder);
        File.Delete(catalogPath);
        Directory.CreateDirectory(catalogPath);

        bool assigned = catalog.TryAssignCaptureId(retainedPath, CaptureId.New());

        Assert.IsFalse(assigned);
        Assert.IsNull(catalog.GetEntries().Single().CaptureId);
    }

    [TestMethod]
    public async Task RemoveAndClear_ShouldNeverDeleteMediaFiles()
    {
        string dataFolder = CreateTestFolder();
        string firstPath = Path.Combine(dataFolder, "Pictures", "first.png");
        string secondPath = Path.Combine(dataFolder, "Documents", "second.png");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        await File.WriteAllTextAsync(firstPath, "first", TestContext.CancellationToken);
        await File.WriteAllTextAsync(secondPath, "second", TestContext.CancellationToken);
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);
        catalog.RecordCaptured(firstPath, CaptureFileType.Image);
        catalog.RecordOpened(secondPath, CaptureFileType.Image);

        Assert.IsTrue(catalog.Remove(firstPath));
        catalog.Clear();

        Assert.IsEmpty(catalog.GetEntries());
        Assert.IsTrue(File.Exists(firstPath));
        Assert.IsTrue(File.Exists(secondPath));
    }

    private static LocalRecentCaptureCatalog CreateCatalog(
        string dataFolder,
        Func<DateTime>? getUtcNow = null,
        TestRecentCapturesChangeNotifier? changeNotifier = null,
        IStorageService? storageService = null)
    {
        return new LocalRecentCaptureCatalog(
            storageService ?? new TestStorageService(dataFolder),
            new TestUserDataProtectionService(),
            new TestClock(getUtcNow),
            new TestLogService(),
            changeNotifier ?? new TestRecentCapturesChangeNotifier());
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetCatalogFilePath(string dataFolder)
    {
        return Path.Combine(dataFolder, "RecentCaptures.json");
    }

    private sealed class TestStorageService(string dataFolder) : IStorageService
    {
        public string GetApplicationDataFolderPath() => dataFolder;
        public string GetApplicationRetainedCaptureFolderPath() => Path.Combine(dataFolder, "Captures");
        public string GetApplicationScratchFolderPath() => Path.Combine(dataFolder, "Temp", "Scratch");
        public string GetSystemDefaultMusicFolderPath() => Path.Combine(dataFolder, "Music");
        public string GetSystemDefaultScreenshotsFolderPath() => Path.Combine(dataFolder, "Pictures");
        public string GetSystemDefaultVideosFolderPath() => Path.Combine(dataFolder, "Videos");
        public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");
    }

    private sealed class ThrowingRetainedFolderStorageService(string dataFolder) : IStorageService
    {
        public string GetApplicationDataFolderPath() => dataFolder;
        public string GetApplicationRetainedCaptureFolderPath() =>
            throw new IOException("Retained folder unavailable.");
        public string GetApplicationScratchFolderPath() => Path.Combine(dataFolder, "Temp", "Scratch");
        public string GetSystemDefaultMusicFolderPath() => Path.Combine(dataFolder, "Music");
        public string GetSystemDefaultScreenshotsFolderPath() => Path.Combine(dataFolder, "Pictures");
        public string GetSystemDefaultVideosFolderPath() => Path.Combine(dataFolder, "Videos");
        public string GetTemporaryFileName() => Guid.NewGuid().ToString("N");
    }

    private sealed class TestClock(Func<DateTime>? getUtcNow) : IClock
    {
        public DateTime Now => UtcNow.ToLocalTime();
        public DateTime UtcNow => getUtcNow?.Invoke() ?? DateTime.UtcNow;
    }

    private sealed class TestUserDataProtectionService : IUserDataProtectionService
    {
        private const byte Mask = 0xA5;

        public byte[] Protect(byte[] plaintext) => Transform(plaintext);

        public byte[] Unprotect(byte[] protectedData) => Transform(protectedData);

        private static byte[] Transform(byte[] value)
        {
            byte[] transformed = new byte[value.Length];
            for (int index = 0; index < value.Length; index++)
            {
                transformed[index] = (byte)(value[index] ^ Mask);
            }

            return transformed;
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

    private sealed class TestRecentCapturesChangeNotifier : IRecentCapturesChangeNotifier
    {
        public int NotificationCount { get; private set; }

        public event EventHandler? RecentCapturesChanged;

        public void NotifyRecentCapturesChanged()
        {
            NotificationCount++;
            RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
