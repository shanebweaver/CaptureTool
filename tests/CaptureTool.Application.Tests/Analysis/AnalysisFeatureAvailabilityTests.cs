using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using Moq;

namespace CaptureTool.Application.Tests.Analysis;

[TestClass]
public sealed class AnalysisFeatureAvailabilityTests
{
    [TestMethod]
    [DataRow(AnalyzerAvailability.Ready, true)]
    [DataRow(AnalyzerAvailability.PreparationRequired, true)]
    [DataRow(AnalyzerAvailability.LanguageResourcesRequired, true)]
    [DataRow(AnalyzerAvailability.Unsupported, false)]
    [DataRow(AnalyzerAvailability.TemporarilyUnavailable, false)]
    public async Task SourceSupportDoesNotRequireConsentOrInstalledModels(AnalyzerAvailability state, bool expected)
    {
        var (availability, source, _) = Create(state);
        Assert.AreEqual(expected, await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        source.Verify(x => x.PrepareAsync(It.IsAny<IProgress<AnalysisProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        source.Verify(x => x.AnalyzeAsync(It.IsAny<AnalysisInput>(), It.IsAny<IProgress<AnalysisProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task OutputNeedsItsProviderAndAtLeastOneDeclaredSource()
    {
        var (availability, source, processor) = Create(AnalyzerAvailability.Unsupported);
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.CaptureName));
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalyzerAvailability.Ready);
        availability.Invalidate();
        // Description is unavailable; OCR alone can supply useful input.
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.CaptureName));
        processor.Setup(x => x.GetAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AnalyzerAvailability.Unsupported);
        availability.Invalidate();
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.CaptureName));
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Audio, AnalysisCapability.CaptureName));
    }

    [TestMethod]
    public async Task ConcurrentReadersShareProbeAndExplicitInvalidationAllowsRecovery()
    {
        var (availability, source, _) = Create(AnalyzerAvailability.Unsupported);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition)));
        source.Verify(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()), Times.Once);
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalyzerAvailability.PreparationRequired);
        availability.Invalidate();
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
    }

    [TestMethod]
    public async Task KnownSupportForOneMediaKindDoesNotExposeAnUnconfirmedKind()
    {
        var (_, source, _) = Create(AnalyzerAvailability.Ready);
        source.SetupGet(x => x.Descriptor).Returns(new MediaAnalyzerDescriptor("ocr", AnalysisCapability.TextRecognition,
            [AnalysisMediaKind.Image, AnalysisMediaKind.Video]));
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Video, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalyzerAvailability.TemporarilyUnavailable);
        var configuration = new CaptureAnalysisConfiguration(new[] { AnalysisMediaKind.Image, AnalysisMediaKind.Video }
            .Select(kind => new MediaAnalysisPlan(kind, "test", [new(AnalysisCapability.TextRecognition, ["ocr"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))])));
        var availability = new AnalysisFeatureAvailability(configuration, [source.Object], []);
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Video, AnalysisCapability.TextRecognition));
    }

    [TestMethod]
    public async Task TransientFailureKeepsKnownSupportedFeatureAvailableForRecovery()
    {
        var (availability, source, _) = Create(AnalyzerAvailability.PreparationRequired);
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalyzerAvailability.TemporarilyUnavailable);
        availability.Invalidate();
        Assert.IsTrue(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalyzerAvailability.Unsupported);
        availability.Invalidate();
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
    }

    [TestMethod]
    public async Task FailingOrHangingProbeDoesNotExposeFeatureOrBlockIndefinitely()
    {
        var (availability, source, _) = Create(AnalyzerAvailability.Ready);
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException());
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition));
        var pending = new TaskCompletionSource<AnalyzerAvailability>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AnalyzerAvailability>(pending.Task));
        availability.Invalidate();
        Assert.IsFalse(await availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition).WaitAsync(TimeSpan.FromSeconds(8)));
        pending.SetResult(AnalyzerAvailability.Ready);
    }

    private static (AnalysisFeatureAvailability, Mock<IMediaAnalyzer>, Mock<IMetadataProcessor>) Create(AnalyzerAvailability state)
    {
        var source = new Mock<IMediaAnalyzer>();
        source.SetupGet(x => x.Descriptor).Returns(new MediaAnalyzerDescriptor("ocr", AnalysisCapability.TextRecognition, [AnalysisMediaKind.Image]));
        source.Setup(x => x.GetAvailabilityAsync(AnalysisMediaKind.Image, null, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var processor = new Mock<IMetadataProcessor>();
        processor.SetupGet(x => x.Descriptor).Returns(new MetadataProcessorDescriptor("name", "1", AnalysisCapability.CaptureName,
            [AnalysisCapability.TextRecognition, AnalysisCapability.Description], new(10, 1000, 100, TimeSpan.FromSeconds(10))));
        processor.Setup(x => x.GetAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AnalyzerAvailability.PreparationRequired);
        var configuration = new CaptureAnalysisConfiguration([new(AnalysisMediaKind.Image, "test", [
            new(AnalysisCapability.TextRecognition, ["ocr"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)),
            new(AnalysisCapability.CaptureName, ["name"], TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10))])]);
        return (new(configuration, [source.Object], [processor.Object]), source, processor);
    }
}
