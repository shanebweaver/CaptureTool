using CaptureTool.Application.Abstractions.Capture.Overlay.CloseCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Overlay.CloseCaptureOverlay;

internal sealed class CloseCaptureOverlayUseCase : ICloseCaptureOverlayUseCase
{
    private const string ActivityId = "CloseCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly ICancelVideoCaptureUseCase _cancelVideoCaptureUseCase;
    private readonly IShowMainWindowUseCase _showMainWindow;
    private readonly INavigationService _navigationService;

    public CloseCaptureOverlayUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        ICancelVideoCaptureUseCase cancelVideoCaptureUseCase,
        IShowMainWindowUseCase showMainWindow,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _cancelVideoCaptureUseCase = cancelVideoCaptureUseCase;
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
            useCase: async token =>
            {
                CaptureOverlayDiscardResult discardResult = await TryDiscardActiveVideoCaptureAsync(token);
                if (!discardResult.CanContinue)
                {
                    return new CloseCaptureOverlayResponse(false);
                }

                await _showMainWindow.ExecuteAsync(new ShowMainWindowRequest(), cancellationToken);

                return new CloseCaptureOverlayResponse(discardResult.VideoCaptureCanceled);
            },
            cancellationToken: cancellationToken);
    }

    private async Task<CaptureOverlayDiscardResult> TryDiscardActiveVideoCaptureAsync(CancellationToken cancellationToken)
    {
        if (!_videoCaptureWorkflow.IsRecording)
        {
            return new(CanContinue: true, VideoCaptureCanceled: false);
        }

        UseCaseResponse<CancelVideoCaptureResponse> response =
            await _cancelVideoCaptureUseCase.ExecuteAsync(new CancelVideoCaptureRequest(), cancellationToken);

        bool canceled = response.Value?.Succeeded == true;
        return new(CanContinue: canceled, VideoCaptureCanceled: canceled);
    }

    private readonly record struct CaptureOverlayDiscardResult(bool CanContinue, bool VideoCaptureCanceled);
}
