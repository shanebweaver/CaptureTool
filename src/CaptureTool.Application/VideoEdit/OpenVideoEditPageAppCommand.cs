using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.VideoEdit;

internal class OpenVideoEditPageAppCommand : IOpenVideoEditPageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenVideoEditPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute(IVideoFile parameter)
    {
        _navigationService.Navigate(NavigationRoute.VideoEdit, parameter);
    }
}
