using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal static class UiTestServiceCollectionExtensions
{
    public static IServiceCollection AddUiTestServices(
        this IServiceCollection services,
        UiTestLaunchOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ILocalizationService, UiTestLocalizationService>();
        services.AddSingleton<IImageCanvasExporter, UiTestImageCanvasExporter>();
        services.AddSingleton<IStorageService, UiTestStorageService>();
        services.AddSingleton<IThemeService, UiTestThemeService>();
        services.AddSingleton<ITextExtractionFeatureAvailability, UiTestTextExtractionFeatureAvailability>();
        services.AddSingleton<ITextExtractionService, UiTestTextExtractionService>();
        services.AddSingleton<ILogService, UiTestFileLogService>();
        services.RemoveAll<IMediaAnalyzer>();
        services.RemoveAll<IMetadataProcessor>();
        services.AddSingleton<IMetadataProcessor, StructuredFactsProcessor>();
        foreach (var model in MetadataEnrichmentConfiguration.SemanticModels)
            services.AddSingleton<IMetadataProcessor>(new UiTestMetadataProcessor(model.Descriptor));
        var metadataIds = MetadataEnrichmentConfiguration.Steps.SelectMany(step => step.Candidates).ToHashSet(StringComparer.Ordinal);
        foreach (var group in CaptureAnalysisConfiguration.CreateDefault().Plans
            .SelectMany(plan => plan.Steps.SelectMany(step => step.Candidates.Select(id => new { Id = id, step.Capability, plan.MediaKind })))
            .Where(candidate => !metadataIds.Contains(candidate.Id))
            .GroupBy(candidate => candidate.Id))
            services.AddSingleton<IMediaAnalyzer>(new UiTestMediaAnalyzer(new(group.Key, group.First().Capability,
                group.Select(candidate => candidate.MediaKind).Distinct().ToArray())));

        return services;
    }

    private sealed class UiTestMetadataProcessor(MetadataProcessorDescriptor descriptor) : IMetadataProcessor
    {
        public MetadataProcessorDescriptor Descriptor => descriptor;
        public Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken cancellationToken) =>
            Task.FromResult(AnalyzerOutcome.Unsuccessful(CaptureTool.Domain.Analysis.AnalyzerOutcomeKind.Unsupported, "ui-test-provider"));
    }
}
