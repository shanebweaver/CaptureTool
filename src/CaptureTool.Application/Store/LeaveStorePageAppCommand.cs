using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Store;

internal class LeaveStorePageAppCommand : ILeaveStorePageAppCommand
{
    private readonly INavigationService _navigationService;

    public LeaveStorePageAppCommand(INavigationService navigationService)
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