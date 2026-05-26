using CaptureTool.Application.Abstractions.ImageEdit;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.ImageEdit;

internal class OpenImageEditPageAppCommand : IOpenImageEditPageAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenImageEditPageAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(IImageFile parameter)
    {
        return true;
    }

    public void Execute(IImageFile parameter)
    {
        _navigationService.Navigate(NavigationRoute.ImageEdit, parameter);
    }
}
