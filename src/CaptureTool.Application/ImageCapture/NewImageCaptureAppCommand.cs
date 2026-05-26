using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.ImageCapture;

internal class NewImageCaptureAppCommand : INewImageCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly CaptureOptions _captureOptions;

    public NewImageCaptureAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _captureOptions = CaptureOptions.ImageDefault;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.ImageCapture, _captureOptions);
    }
}