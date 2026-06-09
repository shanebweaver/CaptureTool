using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Features.Error.RestartApplication;
using CaptureTool.Application.Features.Activation;
using CaptureTool.Application.Features.Error.RestartApplication;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class ActivationServiceCollectionExtensions
{
    public static IServiceCollection AddActivationServices(this IServiceCollection services)
    {
        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();
        services.AddTransient<IRestartApplicationUseCase, RestartApplicationUseCase>();

        return services;
    }
}
