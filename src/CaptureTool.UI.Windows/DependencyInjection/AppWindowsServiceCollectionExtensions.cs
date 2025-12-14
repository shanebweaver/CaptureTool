using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI.Windows.DependencyInjection;

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
