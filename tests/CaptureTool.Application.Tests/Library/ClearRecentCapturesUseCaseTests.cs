using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.ClearRecentCaptures;
using Moq;

namespace CaptureTool.Application.Tests.Library;

[TestClass]
public sealed class ClearRecentCapturesUseCaseTests
{
    [TestMethod]
    public async Task ExecuteAsync_ShouldClearCatalog()
    {
        var catalog = new Mock<IRecentCaptureCatalog>();
        var useCase = new ClearRecentCapturesUseCase(catalog.Object, TestUseCaseExecutor.Instance);

        ClearRecentCapturesResponse response = (await useCase.ExecuteAsync(
            new ClearRecentCapturesRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Cleared);
        catalog.Verify(value => value.Clear(), Times.Once);
    }

    public TestContext TestContext { get; set; } = null!;
}
