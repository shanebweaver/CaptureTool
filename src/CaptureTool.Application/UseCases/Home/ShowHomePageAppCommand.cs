using CaptureTool.Application.Abstractions.Home;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.Home;

internal class ShowHomePageAppCommand : IShowHomePageAppCommand
{
    public ShowHomePageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly INavigationService _navigationService;

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
    }
}
