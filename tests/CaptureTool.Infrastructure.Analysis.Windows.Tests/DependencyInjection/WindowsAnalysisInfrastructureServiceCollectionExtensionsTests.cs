using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsOcrDocumentAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
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
    public void AddWindowsAnalysisDomains_ShouldResolveOcrWithoutImageDescriptionSupport()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITextExtractionAnalysisService, StubTextExtractionAnalysisService>();
        services.AddWindowsAnalysisDomains();
        using ServiceProvider provider = services.BuildServiceProvider();

        ICaptureAnalyzer[] analyzers = [.. provider.GetServices<ICaptureAnalyzer>()];

        Assert.IsTrue(analyzers.Any(analyzer => analyzer is WindowsOcrDocumentAnalyzer));
        Assert.HasCount(2, analyzers);
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
            return TextExtractionReadyState.NotSupported;
        }

        public Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TextExtractionAnalysisResult.Unavailable);
        }
    }
}
