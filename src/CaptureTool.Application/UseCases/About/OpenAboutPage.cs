using CaptureTool.Application.Abstractions.Messaging.Commands;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.About;

internal class OpenAboutPage : IAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenAboutPage(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.About);
    }
}
