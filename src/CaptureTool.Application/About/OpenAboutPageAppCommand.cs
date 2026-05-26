using CaptureTool.Application.Abstractions.About;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.About;

internal class OpenAboutPageAppCommand : IOpenAboutPageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenAboutPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.About);
    }
}
