using CaptureTool.Application.Abstractions.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Store.LeaveStorePage;
using CaptureTool.Application.Store.OpenStorePage;
using CaptureTool.Application.Store.PurchaseChromaKeyAddOn;
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
