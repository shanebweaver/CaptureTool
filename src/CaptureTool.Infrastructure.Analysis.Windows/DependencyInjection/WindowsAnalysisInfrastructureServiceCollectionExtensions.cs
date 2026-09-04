using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Media;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;

public static class WindowsAnalysisInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsAnalysisDomains(this IServiceCollection services)
    {
        services.AddSingleton<IVideoAnalysisFrameSource, WindowsVideoAnalysisFrameSource>();
        services.AddSingleton<IVideoAudioExtractionService, WindowsVideoAudioExtractionService>();
        services.AddSingleton<ICaptureAnalyzer, WindowsImageMediaPropertiesAnalyzer>();
        services.AddSingleton<ICaptureAnalyzer>(provider => new WindowsOcrDocumentAnalyzer(
            GetTextExtractionService(provider, "windows-media-ocr"),
            WindowsOcrDocumentAnalyzer.LegacyAnalyzerId,
            CaptureAnalyzerRequirement.OperatingSystemCapability,
            qualityTier: 60));
        services.AddSingleton<ICaptureAnalyzer>(provider => new WindowsOcrDocumentAnalyzer(
            GetTextExtractionService(provider, "windows-app-sdk-text-recognizer"),
            WindowsOcrDocumentAnalyzer.WindowsAiAnalyzerId,
            CaptureAnalyzerRequirement.OperatingSystemCapability |
                CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            qualityTier: 130));
        services.AddSingleton<ICaptureAnalyzer, WindowsImageDescriptionAnalyzer>();
        services.AddSingleton<ICaptureAnalyzer>(provider => new WindowsVideoOcrTrackAnalyzer(
            provider.GetRequiredService<IVideoAnalysisFrameSource>(),
            GetTextExtractionService(provider, "windows-media-ocr"),
            WindowsVideoOcrTrackAnalyzer.LegacyAnalyzerId,
            CaptureAnalyzerRequirement.OperatingSystemCapability,
            qualityTier: 60));
        services.AddSingleton<ICaptureAnalyzer>(provider => new WindowsVideoOcrTrackAnalyzer(
            provider.GetRequiredService<IVideoAnalysisFrameSource>(),
            GetTextExtractionService(provider, "windows-app-sdk-text-recognizer"),
            WindowsVideoOcrTrackAnalyzer.WindowsAiAnalyzerId,
            CaptureAnalyzerRequirement.OperatingSystemCapability |
                CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            qualityTier: 130));
        services.AddSingleton<ICaptureAnalyzer, WindowsVideoDescriptionTrackAnalyzer>();
        return services;
    }

    private static ITextExtractionAnalysisService GetTextExtractionService(
        IServiceProvider provider,
        string modelId)
    {
        return provider.GetServices<ITextExtractionAnalysisService>().FirstOrDefault(service =>
            string.Equals(service.ModelDescriptor.ModelId, modelId, StringComparison.Ordinal))
            ?? new UnavailableTextExtractionAnalysisService(modelId);
    }

    private sealed class UnavailableTextExtractionAnalysisService(string modelId) :
        ITextExtractionAnalysisService
    {
        public TextExtractionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            modelId,
            null,
            modelId == "windows-app-sdk-text-recognizer"
                ? "windows-app-sdk-ai"
                : "windows-media-ocr",
            null);

        public TextExtractionReadyState GetReadyState() => TextExtractionReadyState.NotSupported;

        public Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TextExtractionAnalysisResult.Unavailable);
    }
}
