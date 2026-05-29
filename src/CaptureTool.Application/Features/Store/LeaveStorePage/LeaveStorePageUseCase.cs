using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Store.LeaveStorePage;

public sealed class LeaveStorePageUseCase : IUseCase<LeaveStorePageRequest, LeaveStorePageResponse>
{
    private readonly INavigationService _navigationService;

    public LeaveStorePageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<LeaveStorePageResponse> ExecuteAsync(LeaveStorePageRequest request, CancellationToken cancellationToken = default)
    {
        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }

        return Task.FromResult(new LeaveStorePageResponse());
    }
}