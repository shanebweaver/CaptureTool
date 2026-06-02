using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Settings.LeaveSettingsPage;

public sealed class LeaveSettingsPageUseCase : ILeaveSettingsPageUseCase
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