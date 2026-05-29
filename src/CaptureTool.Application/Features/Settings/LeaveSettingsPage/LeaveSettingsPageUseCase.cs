using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Settings.LeaveSettingsPage;

public sealed class LeaveSettingsPageUseCase : IUseCase<LeaveSettingsPageRequest, LeaveSettingsPageResponse>
{
    private readonly INavigationService _navigationService;

    public LeaveSettingsPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<LeaveSettingsPageResponse> ExecuteAsync(LeaveSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }

        return Task.FromResult(new LeaveSettingsPageResponse());
    }
}