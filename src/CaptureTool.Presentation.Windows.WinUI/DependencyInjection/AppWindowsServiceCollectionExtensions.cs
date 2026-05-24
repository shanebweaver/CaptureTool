using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI.DependencyInjection;

public static class AppWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddAppWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<AppNavigationHandler>();
        services.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());
        return services;
    }
}
