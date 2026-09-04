using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class WindowsAnalysisInfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWindowsAnalysisDomains_ShouldRegisterBuiltInAnalyzersExplicitly()
    {
        var services = new ServiceCollection();

        services.AddWindowsAnalysisDomains();

        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsImageMediaPropertiesAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.AreEqual(7, services.Count(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsImageDescriptionAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsVideoDescriptionTrackAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IVideoAnalysisFrameSource) &&
            descriptor.ImplementationType == typeof(WindowsVideoAnalysisFrameSource)));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IVideoAudioExtractionService) &&
            descriptor.ImplementationType == typeof(WindowsVideoAudioExtractionService)));
    }

    [TestMethod]
    public void WindowsAnalysisAssembly_ShouldNotReferenceWindowsEditInfrastructure()
    {
#pragma warning disable IL2026 // This architecture test intentionally inspects an untrimmed test assembly.
        string[] references = typeof(WindowsOcrDocumentAnalyzer).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
#pragma warning restore IL2026

        CollectionAssert.DoesNotContain(
            references,
            "CaptureTool.Infrastructure.Edit.Windows");
    }

    [TestMethod]
    public async Task AddWindowsAnalysisDomains_ShouldResolveOcrWithoutImageDescriptionSupport()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITextExtractionAnalysisService, StubTextExtractionAnalysisService>();
        services.AddWindowsAnalysisDomains();
        using ServiceProvider provider = services.BuildServiceProvider();

        ICaptureAnalyzer[] analyzers = [.. provider.GetServices<ICaptureAnalyzer>()];

        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsOcrDocumentAnalyzer));
        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsImageMediaPropertiesAnalyzer));
        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsImageDescriptionAnalyzer));
        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsVideoOcrTrackAnalyzer));
        Assert.IsTrue(analyzers.Any(analyzer =>
            analyzer.Descriptor.Identity.AnalyzerId == WindowsOcrDocumentAnalyzer.WindowsAiAnalyzerId));
        Assert.IsTrue(analyzers.Any(analyzer =>
            analyzer.Descriptor.Identity.AnalyzerId == WindowsVideoOcrTrackAnalyzer.WindowsAiAnalyzerId));
        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsVideoDescriptionTrackAnalyzer));
        Assert.HasCount(7, analyzers);

        var ocr = (WindowsOcrDocumentAnalyzer)analyzers.Single(analyzer =>
            analyzer.Descriptor.Identity.AnalyzerId == WindowsOcrDocumentAnalyzer.LegacyAnalyzerId);
        AnalysisPurpose purpose = new("capture-memory-search", 1);
        CaptureAnalyzerAvailability availability = await ocr.GetAvailabilityAsync(
            new CaptureAnalyzerAvailabilityRequest(
                ocr.Descriptor,
                CaptureMediaKind.Image,
                sourceLength: 1,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose)));
        Assert.AreEqual(CaptureAnalyzerAvailabilityStatus.Available, availability.Status);
    }

    [TestMethod]
    public void PackagedProviderManifest_ShouldMatchRegisteredAnalyzerContracts()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITextExtractionAnalysisService, StubTextExtractionAnalysisService>();
        services.AddWindowsAnalysisDomains();
        using ServiceProvider provider = services.BuildServiceProvider();
        ICaptureAnalyzer[] analyzers = [.. provider.GetServices<ICaptureAnalyzer>()];
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "CaptureAnalysisProviders.json")));
        JsonElement root = document.RootElement;
        JsonElement providerElement = root.GetProperty("providers").EnumerateArray().Single();

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("on-device", root.GetProperty("processingBoundary").GetString());
        Assert.AreEqual("microsoft-windows", providerElement.GetProperty("providerId").GetString());
        Assert.AreEqual(
            "CaptureAnalysis_Provider_MicrosoftWindows",
            providerElement.GetProperty("featureFlag").GetString());

        Dictionary<string, string> manifestCapabilities = providerElement
            .GetProperty("analyzers")
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("analyzerId").GetString()!,
                element => element.GetProperty("capability").GetString()!,
                StringComparer.Ordinal);
        Dictionary<string, string> registeredCapabilities = analyzers.ToDictionary(
            analyzer => analyzer.Descriptor.Identity.AnalyzerId,
            analyzer => $"{analyzer.Descriptor.Capability.Id.Value}/v{analyzer.Descriptor.Capability.SchemaVersion.Value}",
            StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(
            registeredCapabilities.Keys.ToArray(),
            manifestCapabilities.Keys.ToArray());
        foreach ((string analyzerId, string capability) in registeredCapabilities)
        {
            Assert.AreEqual(capability, manifestCapabilities[analyzerId]);
        }
    }

    private sealed class StubTextExtractionAnalysisService : ITextExtractionAnalysisService
    {
        public TextExtractionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-media-ocr",
            ModelVersion: null,
            "windows-media-ocr",
            RuntimeVersion: null);

        public TextExtractionReadyState GetReadyState()
        {
            return TextExtractionReadyState.Ready;
        }

        public Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TextExtractionAnalysisResult.Unavailable);
        }
    }
}
