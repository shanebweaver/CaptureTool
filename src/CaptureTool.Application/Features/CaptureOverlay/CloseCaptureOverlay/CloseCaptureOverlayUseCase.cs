using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Application.Features.Windowing.ShowMainWindow;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;

public sealed class CloseCaptureOverlayUseCase : IUseCase<CloseCaptureOverlayRequest, CloseCaptureOverlayResponse>, IConditional<CloseCaptureOverlayRequest>
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IUseCase<ShowMainWindowRequest, ShowMainWindowResponse> _showMainWindow;
    private readonly INavigationService _navigationService;

    public CloseCaptureOverlayUseCase(
        IVideoCaptureHandler videoCaptureHandler,
        IUseCase<ShowMainWindowRequest, ShowMainWindowResponse> showMainWindow,
        INavigationService navigationService)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _showMainWindow = showMainWindow;
        _navigationService = navigationService;
    }

    public Task<bool> CanExecuteAsync(CloseCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        bool canExecute = _navigationService.CanGoBack
            && _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

        return Task.FromResult(canExecute);
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