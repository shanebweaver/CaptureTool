using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class GetRecentCapturesUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnFiveMostRecentlyWrittenFiles()
    {
        Mock<IStorageService> storageService = new();
        Mock<IFileTypeDetector> fileTypeDetector = new();
        string tempFolder = CreateTestFolder();
        string oldFilePath = Path.Combine(tempFolder, "old.png");
        string recentFilePath = Path.Combine(tempFolder, "recent.png");

        await File.WriteAllTextAsync(oldFilePath, "old", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempFolder, "capture-1.png"), "1", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempFolder, "capture-2.png"), "2", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempFolder, "capture-3.png"), "3", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tempFolder, "capture-4.png"), "4", TestContext.CancellationToken);
        await File.WriteAllTextAsync(recentFilePath, "recent", TestContext.CancellationToken);

        File.SetLastWriteTimeUtc(oldFilePath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(recentFilePath, DateTime.UtcNow);

        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(It.IsAny<string>()))
            .Returns(CaptureFileType.Image);

        GetRecentCapturesUseCase useCase = new(storageService.Object, fileTypeDetector.Object);

        GetRecentCapturesResponse response = await useCase.ExecuteAsync(new GetRecentCapturesRequest(), TestContext.CancellationToken);

        Assert.HasCount(5, response.Captures);
        Assert.AreEqual(recentFilePath, response.Captures[0].FilePath);
        Assert.IsFalse(response.Captures.Any(capture => capture.FilePath == oldFilePath));
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; }
}
