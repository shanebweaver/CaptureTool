using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;
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

    public TestContext TestContext { get; set; } = null!;
}
