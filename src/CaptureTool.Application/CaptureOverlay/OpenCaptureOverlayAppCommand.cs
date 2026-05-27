using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class OpenCaptureOverlayAppCommand : IOpenCaptureOverlayAppCommand
{
    private readonly INavigationService _navigationService;

    public OpenCaptureOverlayAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
    public void Execute(NewCaptureArgs args)
    {
        _navigationService.Navigate(NavigationRoute.CaptureOverlay, args);
    }
}