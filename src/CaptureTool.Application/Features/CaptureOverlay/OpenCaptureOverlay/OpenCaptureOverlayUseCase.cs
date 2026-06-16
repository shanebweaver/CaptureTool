using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;

public sealed class OpenCaptureOverlayUseCase : IOpenCaptureOverlayUseCase
{
    private const string ActivityId = "OpenCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public OpenCaptureOverlayUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<OpenCaptureOverlayResponse>> ExecuteAsync(OpenCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
                return new OpenCaptureOverlayResponse();
            },
            cancellationToken: cancellationToken);
    }
}
