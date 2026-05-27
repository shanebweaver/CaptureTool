using CaptureTool.Application.Abstractions.Messaging.Commands;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.About;

public class LeaveAboutPage : IAppCommand
{
    private readonly INavigationService _navigationService;
    public LeaveAboutPage(INavigationService navigationService)
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