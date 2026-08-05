using CaptureTool.Application.Abstractions.Capture.Overlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Overlay.OpenCaptureOverlay;

internal sealed class OpenCaptureOverlayUseCase : IOpenCaptureOverlayUseCase
{
    private const string ActivityId = "OpenCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenCaptureOverlayUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<OpenCaptureOverlayResponse>> ExecuteAsync(OpenCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.CaptureOverlay,
                    request.CaptureArgs,
                    cancellationToken: cancellationToken);
                return new OpenCaptureOverlayResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
