using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;

internal sealed class OpenSelectionOverlayUseCase : IOpenSelectionOverlayUseCase
{
    private const string ActivityId = "OpenSelectionOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenSelectionOverlayUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<OpenSelectionOverlayResponse>> ExecuteAsync(OpenSelectionOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.SelectionOverlay,
                    request.CaptureOptions,
                    cancellationToken: cancellationToken);
                return new OpenSelectionOverlayResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
