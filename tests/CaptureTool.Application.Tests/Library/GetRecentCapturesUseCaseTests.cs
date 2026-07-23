using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class GetRecentCapturesUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnFiveMostRecentlyWrittenFiles()
    {
        Mock<IStorageService> storageService = new();
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
        GetRecentCapturesUseCase useCase = new(
            storageService.Object,
            TestFileSystem.Instance,
            TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse? response = (await useCase.ExecuteAsync(new GetRecentCapturesRequest(), TestContext.CancellationToken)).Value;

        Assert.IsNotNull(response);
        Assert.HasCount(5, response.Captures);
        Assert.IsTrue(response.HasMore);
        Assert.AreEqual(recentFilePath, response.Captures[0].FilePath);
        Assert.IsFalse(response.Captures.Any(capture => capture.FilePath == oldFilePath));
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldIncludeAudioFilesInRecentCaptures()
    {
        Mock<IStorageService> storageService = new();
        string tempFolder = CreateTestFolder();
        string audioFilePath = Path.Combine(tempFolder, "recent.wav");
        string imageFilePath = Path.Combine(tempFolder, "older.png");

        await File.WriteAllTextAsync(audioFilePath, "audio", TestContext.CancellationToken);
        await File.WriteAllTextAsync(imageFilePath, "image", TestContext.CancellationToken);

        File.SetLastWriteTimeUtc(audioFilePath, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(imageFilePath, DateTime.UtcNow.AddMinutes(-1));

        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);

        GetRecentCapturesUseCase useCase = new(
            storageService.Object,
            TestFileSystem.Instance,
            TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse? response = (await useCase.ExecuteAsync(new GetRecentCapturesRequest(), TestContext.CancellationToken)).Value;

        Assert.IsNotNull(response);
        Assert.HasCount(2, response.Captures);
        Assert.AreEqual(audioFilePath, response.Captures[0].FilePath);
        Assert.AreEqual(imageFilePath, response.Captures[1].FilePath);
        Assert.IsFalse(response.HasMore);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPageRecentCaptures()
    {
        Mock<IStorageService> storageService = new();
        string tempFolder = CreateTestFolder();
        string[] filePaths =
        [
            Path.Combine(tempFolder, "capture-1.png"),
            Path.Combine(tempFolder, "capture-2.png"),
            Path.Combine(tempFolder, "capture-3.png"),
            Path.Combine(tempFolder, "capture-4.png"),
            Path.Combine(tempFolder, "capture-5.png")
        ];

        for (int index = 0; index < filePaths.Length; index++)
        {
            await File.WriteAllTextAsync(filePaths[index], index.ToString(), TestContext.CancellationToken);
            File.SetLastWriteTimeUtc(filePaths[index], DateTime.UtcNow.AddMinutes(-index));
        }

        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(tempFolder);

        GetRecentCapturesUseCase useCase = new(
            storageService.Object,
            TestFileSystem.Instance,
            TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse? response = (await useCase.ExecuteAsync(new GetRecentCapturesRequest(Skip: 2, Take: 2), TestContext.CancellationToken)).Value;

        Assert.IsNotNull(response);
        Assert.HasCount(2, response.Captures);
        Assert.AreEqual(filePaths[2], response.Captures[0].FilePath);
        Assert.AreEqual(filePaths[3], response.Captures[1].FilePath);
        Assert.IsTrue(response.HasMore);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
