using CaptureTool.Application.Abstractions.Features.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Application.Features.About.OpenAboutPage;
using CaptureTool.Application.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Features.AppMenu.OpenFile;
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
