using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Domains.Capture.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.ViewModels.Factories.DependencyInjection;

public static class ViewModelFactoriesServiceCollectionExtensions
{
    public static IServiceCollection AddViewModelFactories(this IServiceCollection services)
    {
        services.AddTransient<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<RecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();
        return services;
    }
}
