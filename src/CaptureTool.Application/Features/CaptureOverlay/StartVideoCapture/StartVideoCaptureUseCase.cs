using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;

public sealed class StartVideoCaptureUseCase : IStartVideoCaptureUseCase
{
    private const string ActivityId = "StartVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StartVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
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
                _videoCaptureHandler.StartVideoCapture(request.CaptureArgs);
                return new StartVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
