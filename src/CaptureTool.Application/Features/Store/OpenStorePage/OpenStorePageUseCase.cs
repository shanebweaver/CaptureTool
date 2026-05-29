using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Store.OpenStorePage;

public sealed class OpenStorePageUseCase : IUseCase<OpenStorePageRequest, OpenStorePageResponse>
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