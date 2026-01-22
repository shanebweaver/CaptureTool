using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Application.Implementations.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels;

namespace CaptureTool.Application.Implementations.DependencyInjection;

public static class ViewModelsServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<IMainWindowViewModel, MainWindowViewModel>();
        services.AddTransient<ISelectionOverlayWindowViewModel, SelectionOverlayWindowViewModel>();
        services.AddTransient<IErrorPageViewModel, ErrorPageViewModel>();
        services.AddTransient<IAboutPageViewModel, AboutPageViewModel>();
        services.AddTransient<IAddOnsPageViewModel, AddOnsPageViewModel>();
        services.AddTransient<IHomePageViewModel, HomePageViewModel>();
        services.AddTransient<ISettingsPageViewModel, SettingsPageViewModel>();
        services.AddTransient<ILoadingPageViewModel, LoadingPageViewModel>();
        services.AddTransient<IImageEditPageViewModel, ImageEditPageViewModel>();
        services.AddTransient<IVideoEditPageViewModel, VideoEditPageViewModel>();
        services.AddTransient<IAppMenuViewModel, AppMenuViewModel>();
        services.AddTransient<IDiagnosticsViewModel, DiagnosticsViewModel>();
        services.AddTransient<ISelectionOverlayHostViewModel, SelectionOverlayHostViewModel>();
        services.AddTransient<ICaptureOverlayViewModel, CaptureOverlayViewModel>();
        return services;
    }
}
