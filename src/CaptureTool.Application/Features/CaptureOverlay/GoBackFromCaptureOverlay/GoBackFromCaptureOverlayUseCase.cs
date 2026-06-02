using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;

public sealed class GoBackFromCaptureOverlayUseCase : IGoBackFromCaptureOverlayUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;

    public GoBackFromCaptureOverlayUseCase(
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
    }

    public bool CanExecute(GoBackFromCaptureOverlayRequest request)
    {
        bool canExecute = _navigationService.CanGoBack
            && _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

        return canExecute;
    }

    public Task<GoBackFromCaptureOverlayResponse> ExecuteAsync(GoBackFromCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _videoCaptureHandler.CancelVideoCapture();
        }
        catch { }

        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true);
        }

        return Task.FromResult(new GoBackFromCaptureOverlayResponse());
    }
}