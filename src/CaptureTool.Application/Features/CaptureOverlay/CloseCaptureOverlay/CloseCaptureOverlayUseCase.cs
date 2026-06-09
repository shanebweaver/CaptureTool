using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;

public sealed class CloseCaptureOverlayUseCase : ICloseCaptureOverlayUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IShowMainWindowUseCase _showMainWindow;
    private readonly INavigationService _navigationService;

    public CloseCaptureOverlayUseCase(
        IVideoCaptureHandler videoCaptureHandler,
        IShowMainWindowUseCase showMainWindow,
        INavigationService navigationService)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _showMainWindow = showMainWindow;
        _navigationService = navigationService;
    }

    public bool CanExecute(CloseCaptureOverlayRequest request)
    {
        bool canExecute = _navigationService.CanGoBack
            && _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

        return canExecute;
    }

    public async Task<CloseCaptureOverlayResponse> ExecuteAsync(CloseCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _videoCaptureHandler.CancelVideoCapture();
        }
        catch { }

        await _showMainWindow.ExecuteAsync(new ShowMainWindowRequest(), cancellationToken);
        return new CloseCaptureOverlayResponse();
    }
}
