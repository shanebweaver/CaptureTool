using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Store.LeaveStorePage;

public sealed class LeaveStorePageUseCase : ILeaveStorePageUseCase
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