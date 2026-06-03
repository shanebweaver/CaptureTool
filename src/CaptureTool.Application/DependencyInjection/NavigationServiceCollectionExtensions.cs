using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Features.Home.ShowHomePage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class NavigationServiceCollectionExtensions
{
    public static IServiceCollection AddNavigationUseCases(this IServiceCollection services)
    {
        services.AddTransient<IShowHomePageUseCase, ShowHomePageUseCase>();

        return services;
    }
}
