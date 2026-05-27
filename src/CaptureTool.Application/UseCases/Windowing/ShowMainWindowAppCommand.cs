using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.Windowing;

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

    public bool CanExecute()
    {
       return _navigationService.CanGoBack;
    }
}
