using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;

public sealed class StartVideoCaptureUseCase : IStartVideoCaptureUseCase
{
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StartVideoCaptureUseCase(
        INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(StartVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<StartVideoCaptureResponse> ExecuteAsync(StartVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _videoCaptureHandler.StartVideoCapture(request.CaptureArgs);
        _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
        return Task.FromResult(new StartVideoCaptureResponse());
    }
}