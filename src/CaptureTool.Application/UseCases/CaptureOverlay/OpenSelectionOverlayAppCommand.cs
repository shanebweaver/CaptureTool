using CaptureTool.Application.Abstractions.Messaging.Commands;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

public class OpenSelectionOverlayAppCommand : IAppCommand<CaptureOptions>
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
