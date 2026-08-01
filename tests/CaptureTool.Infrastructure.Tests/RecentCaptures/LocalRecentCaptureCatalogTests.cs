using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.RecentCaptures;

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

        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder, () => activityUtc);
        catalog.RecordCaptured(capturedPath, CaptureFileType.Image);
        activityUtc = activityUtc.AddMinutes(1);
        catalog.RecordOpened(openedPath, CaptureFileType.Video);

        LocalRecentCaptureCatalog reloadedCatalog = CreateCatalog(dataFolder, () => activityUtc);
        IReadOnlyList<RecentCaptureCatalogEntry> entries = reloadedCatalog.GetEntries();

        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.Any(entry =>
            entry.FilePath == Path.GetFullPath(capturedPath) &&
            entry.Origin == RecentCaptureOrigin.Captured &&
            entry.CaptureFileType == CaptureFileType.Image));
        Assert.IsTrue(entries.Any(entry =>
            entry.FilePath == Path.GetFullPath(openedPath) &&
            entry.Origin == RecentCaptureOrigin.Opened &&
            entry.CaptureFileType == CaptureFileType.Video));
    }

    [TestMethod]
    public void ReplacePath_ShouldTrackAutoSavedFileInsteadOfTemporaryFile()
    {
        string dataFolder = CreateTestFolder();
        string temporaryPath = Path.Combine(dataFolder, "Temp", "capture.wav");
        string savedPath = Path.Combine(dataFolder, "Music", "capture.wav");
        LocalRecentCaptureCatalog catalog = CreateCatalog(dataFolder);

        catalog.RecordCaptured(temporaryPath, CaptureFileType.Audio);
        catalog.ReplacePath(temporaryPath, savedPath);

        IReadOnlyList<RecentCaptureCatalogEntry> entries = catalog.GetEntries();
        Assert.HasCount(1, entries);
        RecentCaptureCatalogEntry entry = entries[0];
        Assert.AreEqual(Path.GetFullPath(savedPath), entry.FilePath);
        Assert.AreEqual(RecentCaptureOrigin.Captured, entry.Origin);
        Assert.AreEqual(CaptureFileType.Audio, entry.CaptureFileType);
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
        Func<DateTime>? getUtcNow = null)
    {
        return new LocalRecentCaptureCatalog(
            new TestStorageService(dataFolder),
            new TestClock(getUtcNow),
            new TestLogService(),
            new TestRecentCapturesChangeNotifier());
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestStorageService(string dataFolder) : IStorageService
    {
        public string GetApplicationDataFolderPath() => dataFolder;
        public string GetApplicationTemporaryFolderPath() => Path.Combine(dataFolder, "Temp");
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
        public event EventHandler? RecentCapturesChanged;
        public void NotifyRecentCapturesChanged() => RecentCapturesChanged?.Invoke(this, EventArgs.Empty);
    }

    public TestContext TestContext { get; set; } = null!;
}
