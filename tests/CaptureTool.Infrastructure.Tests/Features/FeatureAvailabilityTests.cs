using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Features;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class FeatureAvailabilityTests
{
    [TestMethod]
    public void ChromaKeyFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ChromaKeyFeatureAvailability(new ConstantFeatureManager(true)).IsChromaKeyEnabled);
        Assert.IsFalse(new ChromaKeyFeatureAvailability(new ConstantFeatureManager(false)).IsChromaKeyEnabled);
    }

    [TestMethod]
    public void StoreFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new StoreFeatureAvailability(new ConstantFeatureManager(true)).IsStoreEnabled);
        Assert.IsFalse(new StoreFeatureAvailability(new ConstantFeatureManager(false)).IsStoreEnabled);
    }

    [TestMethod]
    public void ImageSuperResolutionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageSuperResolutionFeatureAvailability(new ConstantFeatureManager(true)).IsImageSuperResolutionEnabled);
        Assert.IsFalse(new ImageSuperResolutionFeatureAvailability(new ConstantFeatureManager(false)).IsImageSuperResolutionEnabled);
    }

    [TestMethod]
    public void TextExtractionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new TextExtractionFeatureAvailability(new ConstantFeatureManager(true)).IsTextExtractionEnabled);
        Assert.IsFalse(new TextExtractionFeatureAvailability(new ConstantFeatureManager(false)).IsTextExtractionEnabled);
    }

    private sealed class ConstantFeatureManager : IFeatureManager
    {
        private readonly bool _isEnabled;

        public ConstantFeatureManager(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        public bool IsEnabled(FeatureFlag featureFlag) => _isEnabled;
    }
}
