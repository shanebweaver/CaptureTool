using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CaptureFileAllocatorTests
{
    [TestMethod]
    public void ReserveUniqueFile_WhenFirstNameExists_RetriesWithNextName()
    {
        const string FolderPath = @"C:\Temp";
        string existingPath = Path.Combine(FolderPath, "existing.png");
        string uniquePath = Path.Combine(FolderPath, "unique.png");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(service => service.CreateEmptyFile(existingPath))
            .Throws(new IOException("File exists."));
        fileSystem.Setup(service => service.FileExists(existingPath)).Returns(true);
        var names = new Queue<string>(["existing.png", "unique.png"]);
        var allocator = new CaptureFileAllocator(fileSystem.Object);

        string result = allocator.ReserveUniqueFile(FolderPath, names.Dequeue);

        Assert.AreEqual(uniquePath, result);
        fileSystem.Verify(service => service.CreateEmptyFile(existingPath), Times.Once);
        fileSystem.Verify(service => service.CreateEmptyFile(uniquePath), Times.Once);
    }

    [TestMethod]
    public void CopyToUniqueFile_WhenFirstNameExists_RetriesWithoutOverwrite()
    {
        const string SourcePath = @"C:\Temp\source.png";
        const string FolderPath = @"C:\Captures";
        string existingPath = Path.Combine(FolderPath, "existing.png");
        string uniquePath = Path.Combine(FolderPath, "unique.png");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(service => service.CopyFile(SourcePath, existingPath, false))
            .Throws(new IOException("File exists."));
        fileSystem.Setup(service => service.FileExists(existingPath)).Returns(true);
        var names = new Queue<string>(["existing.png", "unique.png"]);
        var allocator = new CaptureFileAllocator(fileSystem.Object);

        string result = allocator.CopyToUniqueFile(SourcePath, FolderPath, names.Dequeue);

        Assert.AreEqual(uniquePath, result);
        fileSystem.Verify(service => service.CopyFile(SourcePath, existingPath, false), Times.Once);
        fileSystem.Verify(service => service.CopyFile(SourcePath, uniquePath, false), Times.Once);
    }

    [TestMethod]
    public void ReserveUniqueFile_WhenFailureIsNotANameCollision_DoesNotRetry()
    {
        const string FolderPath = @"C:\Temp";
        string path = Path.Combine(FolderPath, "capture.png");
        var expected = new IOException("Disk is full.");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(service => service.CreateEmptyFile(path)).Throws(expected);
        fileSystem.Setup(service => service.FileExists(path)).Returns(false);
        var allocator = new CaptureFileAllocator(fileSystem.Object);

        IOException actual = Assert.ThrowsExactly<IOException>(() =>
            allocator.ReserveUniqueFile(FolderPath, () => "capture.png"));

        Assert.AreSame(expected, actual);
        fileSystem.Verify(service => service.CreateEmptyFile(path), Times.Once);
    }

    [TestMethod]
    public void ReserveUniqueFile_WhenEveryNameExists_StopsAfterBoundedAttempts()
    {
        const string FolderPath = @"C:\Temp";
        string path = Path.Combine(FolderPath, "capture.png");
        var fileSystem = new Mock<IFileSystem>();
        fileSystem
            .Setup(service => service.CreateEmptyFile(path))
            .Throws(new IOException("File exists."));
        fileSystem.Setup(service => service.FileExists(path)).Returns(true);
        var allocator = new CaptureFileAllocator(fileSystem.Object);

        IOException exception = Assert.ThrowsExactly<IOException>(() =>
            allocator.ReserveUniqueFile(FolderPath, () => "capture.png"));

        StringAssert.Contains(exception.Message, CaptureFileAllocator.MaxAttempts.ToString());
        fileSystem.Verify(
            service => service.CreateEmptyFile(path),
            Times.Exactly(CaptureFileAllocator.MaxAttempts));
    }
}
