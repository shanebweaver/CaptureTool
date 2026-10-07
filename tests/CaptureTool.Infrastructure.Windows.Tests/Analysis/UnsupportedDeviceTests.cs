using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Moq;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class UnsupportedDeviceTests
{
    [TestMethod]
    public async Task UnsupportedFoundryNeverResolvesCatalogOrTouchesStorage()
    {
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        using var runtime = new FoundryRuntime(storage.Object, Mock.Of<IAiDeviceSupport>());
        var media = new WindowsAnalysisMedia(Mock.Of<IScratchArtifactStore>());
        IMediaAnalyzer[] analyzers = [new FoundrySpeechAnalyzer("speech", "unused", false, runtime, media),
            new FoundryImageDescriptionAnalyzer("vision", "unused", runtime, media)];
        foreach (var analyzer in analyzers)
        {
            Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.GetAvailabilityAsync(AnalysisMediaKind.Video, null, default));
            Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.PrepareAsync(null, default));
        }
        var descriptor = MetadataEnrichmentConfiguration.SemanticModels.First().Descriptor;
        var processor = new FoundryMetadataProcessor(descriptor, "unused", runtime);
        Assert.AreEqual(AnalyzerAvailability.Unsupported, await processor.GetAvailabilityAsync(default));
        Assert.AreEqual(AnalyzerAvailability.Unsupported, await processor.PrepareAsync(null, default));
        Assert.IsNull(await runtime.ResolveAsync("unused", default));
        storage.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task UnsupportedWindowsAiCannotPrepareOrReadMedia()
    {
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var resources = new Mock<IAnalysisResources>(MockBehavior.Strict);
        foreach (var model in new[] { WindowsImageModel.Ocr, WindowsImageModel.Description })
        {
            var analyzer = new WindowsImageAnalyzer("test", model, AnalysisMediaKind.Image,
                new WindowsAnalysisMedia(Mock.Of<IScratchArtifactStore>()), resources.Object, Mock.Of<IAiDeviceSupport>());
            Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.GetAvailabilityAsync(AnalysisMediaKind.Image, null, default));
            Assert.AreEqual(AnalyzerAvailability.Unsupported, await analyzer.PrepareAsync(null, default));
            var result = await analyzer.AnalyzeAsync(new(CaptureId.New(), AnalysisMediaKind.Image,
                new(new string('a', 64)), @"C:\missing.png"), null, default);
            Assert.AreEqual(AnalyzerOutcomeKind.Unsupported, result.Kind);
        }
        var client = new WindowsLanguageModelClient(resources.Object, new("unused", "unused"), device: Mock.Of<IAiDeviceSupport>());
        Assert.AreEqual(AnalyzerAvailability.Unsupported, client.GetAvailability());
        Assert.AreEqual(AnalyzerAvailability.Unsupported, await client.PrepareAsync(default));
        resources.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }
}
