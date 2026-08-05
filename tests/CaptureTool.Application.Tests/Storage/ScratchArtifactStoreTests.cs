using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Storage;
using Moq;

namespace CaptureTool.Application.Tests.Storage;

[TestClass]
public sealed class ScratchArtifactStoreTests
{
    [TestMethod]
    public async Task ClearUnleasedArtifacts_PreservesActiveLeaseAndRetainedCapture()
    {
        using TestFolders folders = new();
        ScratchArtifactStore store = CreateStore(folders);
        string activePath = store.CreateLeasedArtifactPath("active-editor", ".png");
        string unleasedPath = store.CreateLeasedArtifactPath("clipboard", ".mp4");
        await File.WriteAllTextAsync(activePath, "active", TestContext.CancellationToken);
        await File.WriteAllTextAsync(unleasedPath, "clipboard", TestContext.CancellationToken);
        store.RelinquishArtifact(unleasedPath);

        string retainedCapturePath = Path.Combine(folders.RetainedPath, "only-copy.png");
        Directory.CreateDirectory(folders.RetainedPath);
        await File.WriteAllTextAsync(retainedCapturePath, "capture", TestContext.CancellationToken);

        store.ClearUnleasedArtifacts();

        Assert.IsTrue(File.Exists(activePath));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(unleasedPath)));
        Assert.IsTrue(File.Exists(retainedCapturePath));
    }

    [TestMethod]
    public async Task DeleteArtifact_DeletesOnlyItsOwnerDirectoryAndIsIdempotent()
    {
        using TestFolders folders = new();
        ScratchArtifactStore store = CreateStore(folders);
        string firstPath = store.CreateLeasedArtifactPath("first", ".png");
        string secondPath = store.CreateLeasedArtifactPath("second", ".png");
        await File.WriteAllTextAsync(firstPath, "first", TestContext.CancellationToken);
        await File.WriteAllTextAsync(secondPath, "second", TestContext.CancellationToken);

        store.DeleteArtifact(firstPath);
        store.DeleteArtifact(firstPath);
        store.DeleteArtifact(Path.Combine(folders.RetainedPath, "outside.png"));

        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(firstPath)));
        Assert.IsTrue(File.Exists(secondPath));
    }

    [TestMethod]
    public async Task ScavengeStaleArtifacts_DeletesOnlyStaleUnleasedOwners()
    {
        using TestFolders folders = new();
        DateTime nowUtc = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
        ScratchArtifactStore store = CreateStore(folders, nowUtc);
        string stalePath = store.CreateLeasedArtifactPath("stale", ".tmp");
        string freshPath = store.CreateLeasedArtifactPath("fresh", ".tmp");
        string activePath = store.CreateLeasedArtifactPath("active", ".tmp");
        await File.WriteAllTextAsync(stalePath, "stale", TestContext.CancellationToken);
        await File.WriteAllTextAsync(freshPath, "fresh", TestContext.CancellationToken);
        await File.WriteAllTextAsync(activePath, "active", TestContext.CancellationToken);
        store.RelinquishArtifact(stalePath);
        store.RelinquishArtifact(freshPath);

        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(stalePath)!, nowUtc - TimeSpan.FromDays(8));
        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(freshPath)!, nowUtc - TimeSpan.FromDays(6));
        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(activePath)!, nowUtc - TimeSpan.FromDays(30));

        store.ScavengeStaleArtifacts(TimeSpan.FromDays(7));

        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(stalePath)));
        Assert.IsTrue(File.Exists(freshPath));
        Assert.IsTrue(File.Exists(activePath));
    }

    private static ScratchArtifactStore CreateStore(TestFolders folders, DateTime? utcNow = null)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationScratchFolderPath()).Returns(folders.ScratchPath);
        storage.Setup(service => service.GetTemporaryFileName()).Returns(() => $"{Guid.NewGuid():N}.tmp");
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(utcNow ?? DateTime.UtcNow);
        return new ScratchArtifactStore(
            storage.Object,
            TestFileSystem.Instance,
            clock.Object,
            Mock.Of<ILogService>());
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestFolders : IDisposable
    {
        public TestFolders()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString("N"));
            ScratchPath = Path.Combine(RootPath, "Scratch");
            RetainedPath = Path.Combine(RootPath, "Captures");
        }

        public string RootPath { get; }
        public string ScratchPath { get; }
        public string RetainedPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
    }
}
