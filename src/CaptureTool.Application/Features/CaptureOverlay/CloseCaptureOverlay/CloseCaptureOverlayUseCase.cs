using CaptureTool.Application.Abstractions.Features.CaptureOverlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.CloseCaptureOverlay;

internal sealed class CloseCaptureOverlayUseCase : ICloseCaptureOverlayUseCase
{
    private const string ActivityId = "CloseCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly IShowMainWindowUseCase _showMainWindow;
    private readonly INavigationService _navigationService;

    public CloseCaptureOverlayUseCase(IVideoCaptureWorkflow videoCaptureWorkflow,
        IShowMainWindowUseCase showMainWindow,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _showMainWindow = showMainWindow;
        _navigationService = navigationService;
    }

    public bool CanExecute(CloseCaptureOverlayRequest request)
    {
        bool canExecute = _navigationService.CanGoBack
            && _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

        return canExecute;
    }

    public Task<UseCaseResponse<CloseCaptureOverlayResponse>> ExecuteAsync(CloseCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool videoCaptureCanceled = TryCancelVideoCapture();

                await _showMainWindow.ExecuteAsync(new ShowMainWindowRequest(), cancellationToken);

                return new CloseCaptureOverlayResponse(videoCaptureCanceled);
            },
            cancellationToken: cancellationToken);
    }

    private bool TryCancelVideoCapture()
    {
        try
        {
            _videoCaptureWorkflow.CancelVideoCapture();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
