using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal static class UiTestServiceCollectionExtensions
{
    public static IServiceCollection AddUiTestServices(
        this IServiceCollection services,
        UiTestLaunchOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IStorageService, UiTestStorageService>();
        services.AddSingleton<ILaunchNavigationTargetProvider, UiTestLaunchNavigationTargetProvider>();
        services.AddSingleton<ITextExtractionFeatureAvailability, UiTestTextExtractionFeatureAvailability>();
        services.AddSingleton<ITextExtractionService, UiTestTextExtractionService>();

        return services;
    }
}
