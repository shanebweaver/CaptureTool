using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.ViewModels.DependencyInjection;

public static class ViewModelsServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SelectionOverlayWindowViewModel>();
        services.AddTransient<ErrorPageViewModel>();
        services.AddTransient<AboutPageViewModel>();
        services.AddTransient<AddOnsPageViewModel>();
        services.AddTransient<HomePageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<LoadingPageViewModel>();
        services.AddTransient<ImageEditPageViewModel>();
        services.AddTransient<VideoEditPageViewModel>();
        services.AddTransient<AppMenuViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<SelectionOverlayHostViewModel>();
        services.AddTransient<CaptureOverlayViewModel>();
        return services;
    }
}
