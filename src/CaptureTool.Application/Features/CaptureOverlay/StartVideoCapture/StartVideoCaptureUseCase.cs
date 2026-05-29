using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;

public sealed class StartVideoCaptureUseCase : IUseCase<StartVideoCaptureRequest, StartVideoCaptureResponse>, IConditional<StartVideoCaptureRequest>
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

    public Task<bool> CanExecuteAsync(StartVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return Task.FromResult(canExecute);
    }

    public Task<StartVideoCaptureResponse> ExecuteAsync(StartVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _videoCaptureHandler.StartVideoCapture(request.CaptureArgs);
        _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
        return Task.FromResult(new StartVideoCaptureResponse());
    }
}