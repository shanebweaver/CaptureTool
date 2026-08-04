using CaptureTool.Infrastructure.Files;

namespace CaptureTool.Infrastructure.Tests.Files;

[TestClass]
public sealed class LocalFileSystemTests
{
    [TestMethod]
    public async Task CreateEmptyFile_WhenDestinationExists_PreservesDestination()
    {
        var fileSystem = new LocalFileSystem();
        string root = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolTests",
            Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(root, "capture.png");

        try
        {
            fileSystem.CreateDirectory(root);
            await fileSystem.WriteAllTextAsync(destination, "existing capture", TestContext.CancellationToken);

            Assert.ThrowsExactly<IOException>(() => fileSystem.CreateEmptyFile(destination));

            Assert.AreEqual("existing capture", await File.ReadAllTextAsync(destination, TestContext.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CopyFile_WhenDestinationExistsAndOverwriteIsFalse_PreservesDestination()
    {
        var fileSystem = new LocalFileSystem();
        string root = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolTests",
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source.txt");
        string destination = Path.Combine(root, "destination.txt");

        try
        {
            fileSystem.CreateDirectory(root);
            await fileSystem.WriteAllTextAsync(source, "new capture", TestContext.CancellationToken);
            await fileSystem.WriteAllTextAsync(destination, "existing capture", TestContext.CancellationToken);

            Assert.ThrowsExactly<IOException>(() =>
                fileSystem.CopyFile(source, destination, overwrite: false));

            Assert.AreEqual("existing capture", await File.ReadAllTextAsync(destination, TestContext.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task FileAndDirectoryOperations_RoundTrip()
    {
        var fileSystem = new LocalFileSystem();
        string root = Path.Combine(
            Path.GetTempPath(),
            "CaptureToolTests",
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source.txt");
        string copy = Path.Combine(root, "copy.txt");
        string nested = Path.Combine(root, "nested");

        try
        {
            fileSystem.CreateDirectory(root);
            fileSystem.CreateDirectory(nested);
            await fileSystem.WriteAllTextAsync(
                source,
                "capture",
                TestContext.CancellationToken);

            var lastWriteTime = new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc);
            fileSystem.SetLastWriteTimeUtc(source, lastWriteTime);
            fileSystem.CopyFile(source, copy, overwrite: false);

            Assert.IsTrue(fileSystem.DirectoryExists(root));
            Assert.IsTrue(fileSystem.FileExists(source));
            Assert.AreEqual(lastWriteTime, fileSystem.GetLastWriteTimeUtc(source));
            Assert.HasCount(2, fileSystem.EnumerateFiles(root, "*.txt").ToArray());
            Assert.HasCount(3, fileSystem.EnumerateFileSystemEntries(root).ToArray());

            fileSystem.DeleteFile(copy);
            Assert.IsFalse(fileSystem.FileExists(copy));

            fileSystem.DeleteDirectory(root, recursive: true);
            Assert.IsFalse(fileSystem.DirectoryExists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
