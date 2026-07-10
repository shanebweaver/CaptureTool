using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Overlay.GoBackFromCaptureOverlay;

internal sealed class GoBackFromCaptureOverlayUseCase : IGoBackFromCaptureOverlayUseCase
{
    private const string ActivityId = "GoBackFromCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly ICancelVideoCaptureUseCase _cancelVideoCaptureUseCase;
    private readonly INavigationService _navigationService;

    public GoBackFromCaptureOverlayUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        ICancelVideoCaptureUseCase cancelVideoCaptureUseCase,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _cancelVideoCaptureUseCase = cancelVideoCaptureUseCase;
        _navigationService = navigationService;
    }

    public bool CanExecute(GoBackFromCaptureOverlayRequest request)
    {
        bool canExecute = _navigationService.CanGoBack
            && _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

        return canExecute;
    }

    public Task<UseCaseResponse<GoBackFromCaptureOverlayResponse>> ExecuteAsync(GoBackFromCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                CaptureOverlayDiscardResult discardResult = await TryDiscardActiveVideoCaptureAsync(token);
                if (!discardResult.CanContinue)
                {
                    return new GoBackFromCaptureOverlayResponse(false);
                }

                if (!_navigationService.TryGoBack())
                {
                    _navigationService.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true);
                }

                return new GoBackFromCaptureOverlayResponse(discardResult.VideoCaptureCanceled);
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
