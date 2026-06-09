using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Features.Windowing.ShowMainWindow;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class WindowingServiceCollectionExtensions
{
    public static IServiceCollection AddWindowingUseCases(this IServiceCollection services)
    {
        services.AddTransient<IShowMainWindowUseCase, ShowMainWindowUseCase>();

        return services;
    }
}
