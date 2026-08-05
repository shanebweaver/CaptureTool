using CaptureTool.Application.Abstractions.Capture.Overlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Overlay.GoBackFromCaptureOverlay;

internal sealed class GoBackFromCaptureOverlayUseCase : IGoBackFromCaptureOverlayUseCase
{
    private const string ActivityId = "GoBackFromCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly ICancelVideoCaptureUseCase _cancelVideoCaptureUseCase;
    private readonly INavigationCoordinator _navigationCoordinator;

    public GoBackFromCaptureOverlayUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        ICancelVideoCaptureUseCase cancelVideoCaptureUseCase,
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _cancelVideoCaptureUseCase = cancelVideoCaptureUseCase;
        _navigationCoordinator = navigationCoordinator;
    }

    public bool CanExecute(GoBackFromCaptureOverlayRequest request)
    {
        bool canExecute = _navigationCoordinator.CanGoBack
            && _navigationCoordinator.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;

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

                bool navigated = await _navigationCoordinator.ExecuteTransitionAsync(
                    async transitionToken =>
                    {
                        if (await _navigationCoordinator.TryGoBackAsync(transitionToken))
                        {
                            return true;
                        }

                        return await _navigationCoordinator.NavigateAsync(
                            NavigationRoute.SelectionOverlay,
                            CaptureOptions.VideoDefault,
                            clearHistory: true,
                            cancellationToken: transitionToken);
                    },
                    token);

                if (!navigated)
                {
                    return new GoBackFromCaptureOverlayResponse(false);
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
