using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Features;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class FeatureAvailabilityTests
{
    [TestMethod]
    public void CaptureAnalysisFeatureAvailability_ReturnsPlatformFeatureManagerValue()
    {
        var enabledFeatureManager = new ConstantFeatureManager(true);

        Assert.IsTrue(new CaptureAnalysisFeatureAvailability(
            enabledFeatureManager).IsCaptureAnalysisEnabled);
        Assert.IsFalse(new CaptureAnalysisFeatureAvailability(
            new ConstantFeatureManager(false)).IsCaptureAnalysisEnabled);
        Assert.AreSame(
            AppFeatures.Feature_CaptureAnalysis_Platform,
            enabledFeatureManager.LastFeatureFlag);
    }

    [TestMethod]
    public void CaptureAnalysisFeatureAvailability_HasDeterministicPositiveResolutionRevision()
    {
        var enabled = new CaptureAnalysisFeatureAvailability(new ConstantFeatureManager(true));
        var disabled = new CaptureAnalysisFeatureAvailability(new ConstantFeatureManager(false));

        Assert.IsGreaterThan(0L, enabled.ResolutionPolicyRevision);
        Assert.AreEqual(enabled.ResolutionPolicyRevision, disabled.ResolutionPolicyRevision);
    }

    [TestMethod]
    public void CaptureAnalysisFeatureAvailability_UsesPlatformFlagForEveryProviderAndAnalyzer()
    {
        var enabled = new CaptureAnalysisFeatureAvailability(new ConstantFeatureManager(true));
        var disabled = new CaptureAnalysisFeatureAvailability(new ConstantFeatureManager(false));
        AnalyzerIdentity analyzer = CreateAnalyzerIdentity();

        Assert.IsTrue(enabled.IsProviderEnabled("microsoft.windows-ai"));
        Assert.IsTrue(enabled.IsProviderEnabled("another-provider"));
        Assert.IsTrue(enabled.IsAnalyzerEnabled(analyzer));
        Assert.IsFalse(disabled.IsProviderEnabled("microsoft.windows-ai"));
        Assert.IsFalse(disabled.IsAnalyzerEnabled(analyzer));
    }

    [TestMethod]
    public void CaptureAnalysisFeatureAvailability_RejectsInvalidProviderAndAnalyzerInputs()
    {
        var availability = new CaptureAnalysisFeatureAvailability(new ConstantFeatureManager(true));

        Assert.ThrowsExactly<ArgumentNullException>(() => new CaptureAnalysisFeatureAvailability(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => availability.IsProviderEnabled(null!));
        Assert.ThrowsExactly<ArgumentException>(() => availability.IsProviderEnabled(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => availability.IsProviderEnabled("   "));
        Assert.ThrowsExactly<ArgumentNullException>(() => availability.IsAnalyzerEnabled(null!));
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

    [TestMethod]
    public void ImageForegroundExtractionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageForegroundExtractionFeatureAvailability(new ConstantFeatureManager(true)).IsImageForegroundExtractionEnabled);
        Assert.IsFalse(new ImageForegroundExtractionFeatureAvailability(new ConstantFeatureManager(false)).IsImageForegroundExtractionEnabled);
    }

    [TestMethod]
    [DataRow(ForegroundExtractionReadyState.Ready, true)]
    [DataRow(ForegroundExtractionReadyState.PreparationNeeded, true)]
    [DataRow(ForegroundExtractionReadyState.NotSupported, false)]
    [DataRow(ForegroundExtractionReadyState.Disabled, false)]
    [DataRow(ForegroundExtractionReadyState.Unknown, false)]
    public void ImageForegroundExtractionFeatureAvailability_RequiresRunnableDevice(
        ForegroundExtractionReadyState readyState,
        bool expected)
    {
        var availability = new ImageForegroundExtractionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubImageForegroundExtractionService(readyState));

        Assert.AreEqual(expected, availability.IsImageForegroundExtractionEnabled);
    }

    [TestMethod]
    public void ImageObjectEraseFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageObjectEraseFeatureAvailability(new ConstantFeatureManager(true)).IsImageObjectEraseEnabled);
        Assert.IsFalse(new ImageObjectEraseFeatureAvailability(new ConstantFeatureManager(false)).IsImageObjectEraseEnabled);
    }

    [TestMethod]
    [DataRow(ObjectEraseReadyState.Ready, true)]
    [DataRow(ObjectEraseReadyState.PreparationNeeded, true)]
    [DataRow(ObjectEraseReadyState.NotSupported, false)]
    [DataRow(ObjectEraseReadyState.Disabled, false)]
    [DataRow(ObjectEraseReadyState.Unknown, false)]
    public void ImageObjectEraseFeatureAvailability_RequiresRunnableDevice(
        ObjectEraseReadyState readyState,
        bool expected)
    {
        var availability = new ImageObjectEraseFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubImageObjectEraseService(readyState));

        Assert.AreEqual(expected, availability.IsImageObjectEraseEnabled);
    }

    [TestMethod]
    public void ImageObjectExtractionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new ImageObjectExtractionFeatureAvailability(new ConstantFeatureManager(true)).IsImageObjectExtractionEnabled);
        Assert.IsFalse(new ImageObjectExtractionFeatureAvailability(new ConstantFeatureManager(false)).IsImageObjectExtractionEnabled);
    }

    [TestMethod]
    [DataRow(ForegroundExtractionReadyState.Ready, true)]
    [DataRow(ForegroundExtractionReadyState.PreparationNeeded, true)]
    [DataRow(ForegroundExtractionReadyState.NotSupported, false)]
    [DataRow(ForegroundExtractionReadyState.Disabled, false)]
    [DataRow(ForegroundExtractionReadyState.Unknown, false)]
    public void ImageObjectExtractionFeatureAvailability_RequiresRunnableDevice(
        ForegroundExtractionReadyState readyState,
        bool expected)
    {
        var availability = new ImageObjectExtractionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubImageForegroundExtractionService(readyState));

        Assert.AreEqual(expected, availability.IsImageObjectExtractionEnabled);
    }

    [TestMethod]
    public void VideoSuperResolutionFeatureAvailability_ReturnsFeatureManagerValue()
    {
        Assert.IsTrue(new VideoSuperResolutionFeatureAvailability(
            new ConstantFeatureManager(true)).IsVideoSuperResolutionEnabled);
        Assert.IsFalse(new VideoSuperResolutionFeatureAvailability(
            new ConstantFeatureManager(false)).IsVideoSuperResolutionEnabled);
    }

    [TestMethod]
    [DataRow(VideoSuperResolutionReadyState.Ready, true)]
    [DataRow(VideoSuperResolutionReadyState.PreparationNeeded, true)]
    [DataRow(VideoSuperResolutionReadyState.NotSupported, false)]
    [DataRow(VideoSuperResolutionReadyState.Disabled, false)]
    [DataRow(VideoSuperResolutionReadyState.Unknown, false)]
    public void VideoSuperResolutionFeatureAvailability_RequiresRunnableDevice(
        VideoSuperResolutionReadyState readyState,
        bool expected)
    {
        var availability = new VideoSuperResolutionFeatureAvailability(
            new ConstantFeatureManager(true),
            new StubVideoSuperResolutionService(readyState));

        Assert.AreEqual(expected, availability.IsVideoSuperResolutionEnabled);
    }

    private static AnalyzerIdentity CreateAnalyzerIdentity()
    {
        return new(
            "ocr",
            "microsoft.windows-ai",
            "ocr-model",
            "1",
            "1",
            "windows-ai",
            "1",
            "1",
            null);
    }

    private sealed class ConstantFeatureManager : IFeatureManager
    {
        private readonly bool _isEnabled;

        public ConstantFeatureManager(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        public FeatureFlag? LastFeatureFlag { get; private set; }

        public bool IsEnabled(FeatureFlag featureFlag)
        {
            LastFeatureFlag = featureFlag;
            return _isEnabled;
        }
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

    private sealed class StubImageForegroundExtractionService(ForegroundExtractionReadyState readyState)
        : IImageForegroundExtractionService
    {
        public ForegroundExtractionReadyState GetReadyState() => readyState;

        public Task<ForegroundExtractionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ForegroundExtractionResult> ExtractAsync(
            ForegroundExtractionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubImageObjectEraseService(ObjectEraseReadyState readyState)
        : IImageObjectEraseService
    {
        public ObjectEraseReadyState GetReadyState() => readyState;

        public Task<ObjectErasePreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ObjectEraseResult> EraseAsync(
            ObjectEraseRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubVideoSuperResolutionService(VideoSuperResolutionReadyState readyState)
        : IVideoSuperResolutionService
    {
        public VideoSuperResolutionReadyState GetReadyState() => readyState;

        public Task<VideoSuperResolutionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<VideoSuperResolutionResult> GenerateAsync(
            VideoSuperResolutionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
