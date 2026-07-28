using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.DeleteRecentCapture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Library.RecentCaptures.DeleteRecentCapture;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class DeleteRecentCaptureUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldDeleteCaptureFromTemporaryFolder()
    {
        string temporaryFolderPath = CreateTestFolder();
        string capturePath = Path.Combine(temporaryFolderPath, "capture.png");
        await File.WriteAllTextAsync(capturePath, "capture", TestContext.CancellationToken);
        var storageService = new Mock<IStorageService>();
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(temporaryFolderPath);
        var recentCapturesChangeNotifier = new Mock<IRecentCapturesChangeNotifier>();
        var useCase = new DeleteRecentCaptureUseCase(
            TestFileSystem.Instance,
            storageService.Object,
            TestUseCaseExecutor.Instance,
            recentCapturesChangeNotifier.Object);

        DeleteRecentCaptureResponse response = (await useCase.ExecuteAsync(
            new DeleteRecentCaptureRequest(capturePath),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Deleted);
        Assert.IsFalse(File.Exists(capturePath));
        recentCapturesChangeNotifier.Verify(
            notifier => notifier.NotifyRecentCapturesChanged(),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotDeleteFileOutsideTemporaryFolder()
    {
        string rootFolderPath = CreateTestFolder();
        string temporaryFolderPath = Path.Combine(rootFolderPath, "Temp");
        Directory.CreateDirectory(temporaryFolderPath);
        string outsideFilePath = Path.Combine(rootFolderPath, "outside.png");
        await File.WriteAllTextAsync(outsideFilePath, "outside", TestContext.CancellationToken);
        var storageService = new Mock<IStorageService>();
        storageService
            .Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(temporaryFolderPath);
        var recentCapturesChangeNotifier = new Mock<IRecentCapturesChangeNotifier>();
        var useCase = new DeleteRecentCaptureUseCase(
            TestFileSystem.Instance,
            storageService.Object,
            TestUseCaseExecutor.Instance,
            recentCapturesChangeNotifier.Object);

        DeleteRecentCaptureResponse response = (await useCase.ExecuteAsync(
            new DeleteRecentCaptureRequest(outsideFilePath),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Deleted);
        Assert.IsTrue(File.Exists(outsideFilePath));
        recentCapturesChangeNotifier.Verify(
            notifier => notifier.NotifyRecentCapturesChanged(),
            Times.Never);
    }

    private static string CreateTestFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public TestContext TestContext { get; set; } = null!;
}
