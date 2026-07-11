using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;
using CaptureTool.Application.Activation;
using CaptureTool.Application.Shell.Error.RestartApplication;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class ActivationServiceCollectionExtensions
{
    public static IServiceCollection AddActivationServices(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationStartupInitializer, ApplicationStartupInitializer>();
        services.AddSingleton<ILaunchNavigationTargetProvider, DefaultLaunchNavigationTargetProvider>();
        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();
        services.AddTransient<IRestartApplicationUseCase, RestartApplicationUseCase>();

        return services;
    }
}
