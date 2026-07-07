using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GoBackFromCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.GoBackFromCaptureOverlay;

internal sealed class GoBackFromCaptureOverlayUseCase : IGoBackFromCaptureOverlayUseCase
{
    private const string ActivityId = "GoBackFromCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly INavigationService _navigationService;

    public GoBackFromCaptureOverlayUseCase(IVideoCaptureWorkflow videoCaptureWorkflow,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
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
            useCase: () =>
            {
                bool videoCaptureCanceled = TryCancelVideoCapture();

                if (!_navigationService.TryGoBack())
                {
                    _navigationService.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true);
                }

                return new GoBackFromCaptureOverlayResponse(videoCaptureCanceled);
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
