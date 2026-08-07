using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Analysis.Memory;
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
        services.AddSingleton<IThemeService, UiTestThemeService>();
        services.AddSingleton<ITextExtractionFeatureAvailability, UiTestTextExtractionFeatureAvailability>();
        services.AddSingleton<ITextExtractionService, UiTestTextExtractionService>();
        services.AddSingleton<ILogService, UiTestFileLogService>();

        if (options.IsCaptureMemoryEnabled)
        {
            var captureMemory = new UiTestCaptureMemoryService(options);
            services.AddSingleton<ICaptureMemoryFeatureAvailability>(captureMemory);
            services.AddSingleton<ICaptureAnalysisPolicyService>(captureMemory);
            services.AddSingleton<ICaptureAnalysisPolicyCommandService>(captureMemory);
            services.AddSingleton<IUserInitiatedAnalysisCapabilityPreparationService>(captureMemory);
            services.AddSingleton<ICaptureMemorySearchService>(captureMemory);
            services.AddSingleton<ICaptureMemoryResultResolver>(captureMemory);
            services.AddSingleton<IOpenCaptureMemoryResultUseCase>(captureMemory);
            services.AddSingleton<ICaptureAssetRemovalService>(captureMemory);
            services.AddSingleton<ICaptureAnalysisMaintenanceService>(captureMemory);
        }

        return services;
    }
}
