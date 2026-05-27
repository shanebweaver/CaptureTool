using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class OpenSelectionOverlayAppCommand : IOpenSelectionOverlayAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenSelectionOverlayAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Execute(CaptureOptions parameter)
    {
        _navigationService.Navigate(NavigationRoute.SelectionOverlay, parameter);
    }
}
