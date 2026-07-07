using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;

internal sealed class StartVideoCaptureUseCase : IStartVideoCaptureUseCase
{
    private const string ActivityId = "StartVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public StartVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public bool CanExecute(StartVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<UseCaseResponse<StartVideoCaptureResponse>> ExecuteAsync(StartVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _videoCaptureWorkflow.StartVideoCapture(request.CaptureArgs);
                return new StartVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
