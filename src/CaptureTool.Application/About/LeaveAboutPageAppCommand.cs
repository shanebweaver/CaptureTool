using CaptureTool.Application.Abstractions.About;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.About;

internal class LeaveAboutPageAppCommand : ILeaveAboutPageAppCommand
{
    private readonly INavigationService _navigationService;
    public LeaveAboutPageAppCommand(INavigationService navigationService)
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