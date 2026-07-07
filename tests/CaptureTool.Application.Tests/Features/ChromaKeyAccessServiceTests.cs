using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Features.ImageEdit.ChromaKey;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class ChromaKeyAccessServiceTests
{
    [TestMethod]
    public void IsChromaKeyEnabled_DelegatesToFeatureAvailability()
    {
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(true);

        var service = new ChromaKeyAccessService(featureAvailability.Object, Mock.Of<IStoreService>());

        Assert.IsTrue(service.IsChromaKeyEnabled);
    }

    [TestMethod]
    public async Task IsChromaKeyAddOnOwnedAsync_WhenFeatureDisabled_ReturnsFalseWithoutQueryingStore()
    {
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        var storeService = new Mock<IStoreService>();

        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(false);

        var service = new ChromaKeyAccessService(featureAvailability.Object, storeService.Object);

        bool isOwned = await service.IsChromaKeyAddOnOwnedAsync(TestContext.CancellationToken);

        Assert.IsFalse(isOwned);
        storeService.Verify(
            service => service.IsAddonPurchasedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task IsChromaKeyAddOnOwnedAsync_WhenFeatureEnabled_QueriesChromaKeyAddOn()
    {
        var featureAvailability = new Mock<IChromaKeyFeatureAvailability>();
        var storeService = new Mock<IStoreService>();

        featureAvailability
            .Setup(service => service.IsChromaKeyEnabled)
            .Returns(true);
        storeService
            .Setup(service => service.IsAddonPurchasedAsync(
                CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval,
                TestContext.CancellationToken))
            .ReturnsAsync(true);

        var service = new ChromaKeyAccessService(featureAvailability.Object, storeService.Object);

        bool isOwned = await service.IsChromaKeyAddOnOwnedAsync(TestContext.CancellationToken);

        Assert.IsTrue(isOwned);
    }

    public TestContext TestContext { get; set; } = null!;
}
