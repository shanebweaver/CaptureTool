using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Settings;

internal class OpenSettingsPageAppCommand : IOpenSettingsPageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenSettingsPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.Settings);
    }
}
