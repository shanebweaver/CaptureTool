using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class GetRecentCapturesUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldCombineCapturedAndOpenedFilesAndSortByCatalogActivity()
    {
        string imagePath = await CreateFileAsync("Pictures", "capture.png");
        string videoPath = await CreateFileAsync("Videos", "capture.mp4");
        string audioPath = await CreateFileAsync("Music", "capture.wav");
        string openedPath = await CreateFileAsync("Documents", "opened.jpg");
        File.SetLastWriteTimeUtc(openedPath, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var entries = new[]
        {
            Entry(imagePath, CaptureFileType.Image, RecentCaptureOrigin.Captured, 1),
            Entry(videoPath, CaptureFileType.Video, RecentCaptureOrigin.Captured, 2),
            Entry(audioPath, CaptureFileType.Audio, RecentCaptureOrigin.Captured, 3),
            Entry(openedPath, CaptureFileType.Image, RecentCaptureOrigin.Opened, 4),
        };
        var catalog = new Mock<IRecentCaptureCatalog>();
        catalog.Setup(value => value.GetEntries()).Returns(entries);
        var useCase = new GetRecentCapturesUseCase(
            catalog.Object,
            TestFileSystem.Instance,
            TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse response = (await useCase.ExecuteAsync(
            new GetRecentCapturesRequest(Take: 10),
            TestContext.CancellationToken)).Value!;

        CollectionAssert.AreEqual(
            new[] { openedPath, audioPath, videoPath, imagePath },
            response.Captures.Select(capture => capture.FilePath).ToArray());
        Assert.IsFalse(response.HasMore);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPageCatalogEntries()
    {
        string[] filePaths =
        [
            await CreateFileAsync("Pictures", "capture-1.png"),
            await CreateFileAsync("Pictures", "capture-2.png"),
            await CreateFileAsync("Pictures", "capture-3.png"),
            await CreateFileAsync("Pictures", "capture-4.png"),
            await CreateFileAsync("Pictures", "capture-5.png"),
        ];
        var entries = filePaths
            .Select((path, index) => Entry(path, CaptureFileType.Image, RecentCaptureOrigin.Captured, 5 - index))
            .ToArray();
        var catalog = new Mock<IRecentCaptureCatalog>();
        catalog.Setup(value => value.GetEntries()).Returns(entries);
        var useCase = new GetRecentCapturesUseCase(catalog.Object, TestFileSystem.Instance, TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse response = (await useCase.ExecuteAsync(
            new GetRecentCapturesRequest(Skip: 2, Take: 2),
            TestContext.CancellationToken)).Value!;

        CollectionAssert.AreEqual(
            filePaths.Skip(2).Take(2).ToArray(),
            response.Captures.Select(capture => capture.FilePath).ToArray());
        Assert.IsTrue(response.HasMore);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPruneMissingCatalogEntries()
    {
        string existingPath = await CreateFileAsync("Pictures", "existing.png");
        string missingPath = Path.Combine(Path.GetDirectoryName(existingPath)!, "missing.png");
        var catalog = new Mock<IRecentCaptureCatalog>();
        catalog.Setup(value => value.GetEntries()).Returns(
        [
            Entry(missingPath, CaptureFileType.Image, RecentCaptureOrigin.Opened, 2),
            Entry(existingPath, CaptureFileType.Image, RecentCaptureOrigin.Captured, 1),
        ]);
        var useCase = new GetRecentCapturesUseCase(catalog.Object, TestFileSystem.Instance, TestUseCaseExecutor.Instance);

        GetRecentCapturesResponse response = (await useCase.ExecuteAsync(
            new GetRecentCapturesRequest(Take: 10),
            TestContext.CancellationToken)).Value!;

        Assert.HasCount(1, response.Captures);
        Assert.AreEqual(existingPath, response.Captures[0].FilePath);
        catalog.Verify(value => value.RemoveRange(
            It.Is<IEnumerable<string>>(paths => paths.SequenceEqual(new[] { missingPath }))), Times.Once);
    }

    private static RecentCaptureCatalogEntry Entry(
        string filePath,
        CaptureFileType fileType,
        RecentCaptureOrigin origin,
        int minute)
    {
        return new(filePath, fileType, origin, new DateTime(2026, 1, 1, 0, minute, 0, DateTimeKind.Utc));
    }

    private static async Task<string> CreateFileAsync(string folderName, string fileName)
    {
        string folderPath = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString(), folderName);
        Directory.CreateDirectory(folderPath);
        string filePath = Path.Combine(folderPath, fileName);
        await File.WriteAllTextAsync(filePath, "capture");
        return filePath;
    }

    public TestContext TestContext { get; set; } = null!;
}
