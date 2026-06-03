using CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Features.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Features.Store.LeaveStorePage;
using CaptureTool.Application.Features.Store.OpenStorePage;
using CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class StoreServiceCollectionExtensions
{
    public static IServiceCollection AddStoreUseCases(this IServiceCollection services)
    {
        services.AddTransient<IGetChromaKeyAddOnUseCase, GetChromaKeyAddOnUseCase>();
        services.AddTransient<IPurchaseChromaKeyAddOnUseCase, PurchaseChromaKeyAddOnUseCase>();
        services.AddTransient<IOpenStorePageUseCase, OpenStorePageUseCase>();
        services.AddTransient<ILeaveStorePageUseCase, LeaveStorePageUseCase>();

        return services;
    }
}
