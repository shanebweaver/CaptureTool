using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Windowing;

internal class ShowMainWindowAppCommand : IShowMainWindowAppCommand
{
    public ShowMainWindowAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly INavigationService _navigationService;

    public void Execute()
    {
        bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route));
        if (!success)
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }
    }
}
