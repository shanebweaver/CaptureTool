using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Settings;

internal class LeaveSettingsPageAppCommand : ILeaveSettingsPageAppCommand
{
    private readonly INavigationService _navigationService;

    public LeaveSettingsPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute()
    {
        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }
    }
}