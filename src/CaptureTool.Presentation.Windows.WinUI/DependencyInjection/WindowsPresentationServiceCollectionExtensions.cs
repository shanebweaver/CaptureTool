using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI.DependencyInjection;

public static class WindowsPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddAppWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<AppNavigationHandler>();
        services.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());
        return services;
    }
}
