using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.AppMenu;

internal class ShowSettingsAppCommand : IShowSettingsAppCommand
{
    private readonly INavigationService _navigationService;

    public ShowSettingsAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _navigationService.Navigate(CaptureToolNavigationRoute.Settings);
    }
}
