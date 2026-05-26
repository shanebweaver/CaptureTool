using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Store;

internal class OpenStorePageAppCommand : IOpenStorePageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenStorePageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.Store);
    }
}
