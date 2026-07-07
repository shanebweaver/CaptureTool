using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;

internal sealed class StopVideoCaptureUseCase : IStopVideoCaptureUseCase
{
    private const string ActivityId = "StopVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public StopVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public bool CanExecute(StopVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<UseCaseResponse<StopVideoCaptureResponse>> ExecuteAsync(StopVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var pendingVideo = _videoCaptureWorkflow.StopVideoCapture();
                _navigationService.Navigate(NavigationRoute.VideoEdit, pendingVideo);
                return new StopVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
