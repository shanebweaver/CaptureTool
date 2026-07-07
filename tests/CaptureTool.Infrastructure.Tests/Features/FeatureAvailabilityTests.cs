using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Features;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class FeatureAvailabilityTests
{
    [TestMethod]
    public void AudioCaptureFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new AudioCaptureFeatureAvailability(new ConstantFeatureManager(true)).IsAudioCaptureEnabled);
        Assert.IsFalse(new AudioCaptureFeatureAvailability(new ConstantFeatureManager(false)).IsAudioCaptureEnabled);
    }

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
