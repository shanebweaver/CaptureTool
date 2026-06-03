using CaptureTool.Application.Abstractions.Features.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.About.LeaveAboutPage;

public sealed class LeaveAboutPageUseCase : ILeaveAboutPageUseCase
{
    private readonly INavigationService _navigationService;

    public LeaveAboutPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<LeaveAboutPageResponse> ExecuteAsync(LeaveAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }

        return Task.FromResult(new LeaveAboutPageResponse());
    }
}