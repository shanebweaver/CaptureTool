using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class RemoveRecentCaptureUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldRemoveCatalogEntryWithoutDeletingFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"capture-{Guid.NewGuid():N}.png");
        await File.WriteAllTextAsync(filePath, "capture", TestContext.CancellationToken);
        var catalog = new Mock<IRecentCaptureCatalog>();
        catalog.Setup(value => value.Remove(filePath)).Returns(true);
        var useCase = new RemoveRecentCaptureUseCase(catalog.Object, TestUseCaseExecutor.Instance);

        RemoveRecentCaptureResponse response = (await useCase.ExecuteAsync(
            new RemoveRecentCaptureRequest(filePath),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Removed);
        Assert.IsTrue(File.Exists(filePath));
        catalog.Verify(value => value.Remove(filePath), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_ForIdentifiedCapture_ShouldUseTombstoneFirstForgetWorkflow()
    {
        string filePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capture.png"));
        CaptureId captureId = CaptureId.New();
        var catalog = new Mock<IRecentCaptureCatalog>(MockBehavior.Strict);
        catalog.Setup(value => value.GetEntries()).Returns(
        [
            new RecentCaptureCatalogEntry(
                filePath,
                CaptureFileType.Image,
                RecentCaptureOrigin.Captured,
                DateTime.UtcNow,
                captureId),
        ]);
        var removal = new Mock<ICaptureAssetRemovalService>(MockBehavior.Strict);
        removal.Setup(service => service.RemoveAsync(
                It.Is<CaptureAssetRemovalRequest>(request =>
                    request.CaptureId == captureId &&
                    request.Kind == CaptureAssetRemovalKind.ForgetHistory),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new CaptureAssetRemovalResult(
                CaptureAssetRemovalStatus.Succeeded,
                new CaptureAssetRemovalRequest(captureId, CaptureAssetRemovalKind.ForgetHistory))));
        var useCase = new RemoveRecentCaptureUseCase(
            catalog.Object,
            TestUseCaseExecutor.Instance,
            removal.Object);

        RemoveRecentCaptureResponse response = (await useCase.ExecuteAsync(
            new RemoveRecentCaptureRequest(filePath),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Removed);
        removal.VerifyAll();
        catalog.VerifyAll();
        catalog.Verify(value => value.Remove(It.IsAny<string>()), Times.Never);
    }

    public TestContext TestContext { get; set; } = null!;
}
