using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shell.AppMenu.OpenFile;
using CaptureTool.Application.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Shell.About.OpenAboutPage;
using CaptureTool.Application.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Shell.AppMenu.OpenFile;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class AppMenuServiceCollectionExtensions
{
    public static IServiceCollection AddAppMenuUseCases(this IServiceCollection services)
    {
        services.AddTransient<ILeaveAboutPageUseCase, LeaveAboutPageUseCase>();
        services.AddTransient<IOpenAboutPageUseCase, OpenAboutPageUseCase>();
        services.AddTransient<IExitApplicationUseCase, ExitApplicationUseCase>();
        services.AddTransient<IOpenFileUseCase, OpenFileUseCase>();

        return services;
    }
}
