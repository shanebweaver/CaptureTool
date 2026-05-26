using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.VideoCapture;

internal class NewVideoCaptureAppCommand : INewVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly CaptureOptions _captureOptions;

    public NewVideoCaptureAppCommand(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _captureOptions = CaptureOptions.VideoDefault;
    }

    public void Execute()
    {
        _navigationService.Navigate(NavigationRoute.ImageCapture, _captureOptions);
    }
}