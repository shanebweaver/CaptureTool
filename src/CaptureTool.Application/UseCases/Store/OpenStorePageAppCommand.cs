using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.Store;

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
