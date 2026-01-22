using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Themes;
using CaptureTool.Domains.Capture.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.Implementations.ViewModels.Factories.DependencyInjection;

public static class ViewModelFactoriesServiceCollectionExtensions
{
    public static IServiceCollection AddViewModelFactories(this IServiceCollection services)
    {
        services.AddTransient<IFactoryServiceWithArgs<IAppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<IAppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<ICaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<ICaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        services.AddTransient<IFactoryServiceWithArgs<IRecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();
        return services;
    }
}
