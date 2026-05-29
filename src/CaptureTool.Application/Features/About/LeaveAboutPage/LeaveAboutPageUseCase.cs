using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.About.LeaveAboutPage;

public sealed class LeaveAboutPageUseCase : IUseCase<LeaveAboutPageRequest, LeaveAboutPageResponse>
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