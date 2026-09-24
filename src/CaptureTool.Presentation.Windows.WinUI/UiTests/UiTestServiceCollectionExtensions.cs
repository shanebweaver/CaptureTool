using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.FeatureManagement;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IApplicationLocalCachePathProvider, UiTestApplicationLocalCachePathProvider>();
        services.AddSingleton<IThemeService, UiTestThemeService>();
        services.AddSingleton<ITextExtractionFeatureAvailability, UiTestTextExtractionFeatureAvailability>();
        services.AddSingleton<ITextExtractionService, UiTestTextExtractionService>();
        services.AddSingleton<ILogService, UiTestFileLogService>();

        if (options.IsCaptureAnalysisEnabled)
        {
            services.AddSingleton<IFeatureManager, UiTestCaptureAnalysisFeatureManager>();
        }

        if (options.IsCaptureMemoryEnabled)
        {
            services.AddSingleton(provider => new UiTestCaptureMemoryService(options,
                () => provider.GetRequiredService<IOpenImageEditPageUseCase>()));
            services.AddSingleton<ICaptureMemoryFeatureAvailability>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAnalysisPolicyService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAnalysisPolicyCommandService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAnalysisBackfillService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAnalysisActivityQueryService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<IUserInitiatedAnalysisCapabilityPreparationService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureMemorySearchService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureMemoryResultResolver>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<IOpenCaptureMemoryResultUseCase>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAssetRemovalService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureMetadataViewService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<IAnalysisCapabilityPreparationQueryService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
            services.AddSingleton<ICaptureAnalysisMaintenanceService>(provider => provider.GetRequiredService<UiTestCaptureMemoryService>());
        }

        return services;
    }
}
