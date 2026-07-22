using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
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
    public void AiConsentSettingsFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new AiConsentSettingsFeatureAvailability(new ConstantFeatureManager(true)).IsAiConsentSettingsEnabled);
        Assert.IsFalse(new AiConsentSettingsFeatureAvailability(new ConstantFeatureManager(false)).IsAiConsentSettingsEnabled);
    }

    [TestMethod]
    public void ImageSuperResolutionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageSuperResolutionFeatureAvailability(new ConstantFeatureManager(true)).IsImageSuperResolutionEnabled);
        Assert.IsFalse(new ImageSuperResolutionFeatureAvailability(new ConstantFeatureManager(false)).IsImageSuperResolutionEnabled);
    }

    [TestMethod]
    [DataRow(ImageSuperResolutionReadyState.Ready, true)]
    [DataRow(ImageSuperResolutionReadyState.PreparationNeeded, true)]
    [DataRow(ImageSuperResolutionReadyState.NotSupported, false)]
    [DataRow(ImageSuperResolutionReadyState.Disabled, false)]
    [DataRow(ImageSuperResolutionReadyState.Unknown, false)]
    public void ImageSuperResolutionFeatureAvailability_RequiresRunnableDevice(
        ImageSuperResolutionReadyState readyState,
        bool expected)
    {
        var availability = new ImageSuperResolutionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubImageSuperResolutionService(readyState));

        Assert.AreEqual(expected, availability.IsImageSuperResolutionEnabled);
    }

    [TestMethod]
    public void TextExtractionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new TextExtractionFeatureAvailability(new ConstantFeatureManager(true)).IsTextExtractionEnabled);
        Assert.IsFalse(new TextExtractionFeatureAvailability(new ConstantFeatureManager(false)).IsTextExtractionEnabled);
    }

    [TestMethod]
    [DataRow(TextExtractionReadyState.Ready, true)]
    [DataRow(TextExtractionReadyState.PreparationNeeded, true)]
    [DataRow(TextExtractionReadyState.NotSupported, false)]
    [DataRow(TextExtractionReadyState.Disabled, false)]
    [DataRow(TextExtractionReadyState.Unknown, false)]
    public void TextExtractionFeatureAvailability_RequiresRunnableDevice(
        TextExtractionReadyState readyState,
        bool expected)
    {
        var availability = new TextExtractionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubTextExtractionService(readyState));

        Assert.AreEqual(expected, availability.IsTextExtractionEnabled);
    }

    [TestMethod]
    public void ImageDescriptionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageDescriptionFeatureAvailability(new ConstantFeatureManager(true)).IsImageDescriptionEnabled);
        Assert.IsFalse(new ImageDescriptionFeatureAvailability(new ConstantFeatureManager(false)).IsImageDescriptionEnabled);
    }

    [TestMethod]
    [DataRow(ImageDescriptionReadyState.Ready, true)]
    [DataRow(ImageDescriptionReadyState.PreparationNeeded, true)]
    [DataRow(ImageDescriptionReadyState.NotSupported, false)]
    [DataRow(ImageDescriptionReadyState.Disabled, false)]
    [DataRow(ImageDescriptionReadyState.Unknown, false)]
    public void ImageDescriptionFeatureAvailability_RequiresRunnableDevice(
        ImageDescriptionReadyState readyState,
        bool expected)
    {
        var availability = new ImageDescriptionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubImageDescriptionService(readyState));

        Assert.AreEqual(expected, availability.IsImageDescriptionEnabled);
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

    private sealed class StubImageSuperResolutionService(ImageSuperResolutionReadyState readyState)
        : IImageSuperResolutionService
    {
        public ImageSuperResolutionReadyState GetReadyState() => readyState;

        public Task<ImageSuperResolutionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ImageSuperResolutionResult> GenerateAsync(
            ImageSuperResolutionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubTextExtractionService(TextExtractionReadyState readyState) : ITextExtractionService
    {
        public TextExtractionReadyState GetReadyState() => readyState;

        public Task<TextExtractionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TextExtractionResult> ExtractAsync(
            TextExtractionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubImageDescriptionService(ImageDescriptionReadyState readyState) : IImageDescriptionService
    {
        public ImageDescriptionReadyState GetReadyState() => readyState;

        public Task<ImageDescriptionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ImageDescriptionResult> DescribeAsync(
            ImageDescriptionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
