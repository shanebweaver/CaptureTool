using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Store.OpenStorePage;

public sealed class OpenStorePageUseCase : IOpenStorePageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenStorePageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenStorePageResponse> ExecuteAsync(OpenStorePageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.Store);
        return Task.FromResult(new OpenStorePageResponse());
    }
}