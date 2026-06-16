using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;

public sealed class OpenSelectionOverlayUseCase : IOpenSelectionOverlayUseCase
{
    private const string ActivityId = "OpenSelectionOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public OpenSelectionOverlayUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<OpenSelectionOverlayResponse>> ExecuteAsync(OpenSelectionOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.SelectionOverlay, request.CaptureOptions);
                return new OpenSelectionOverlayResponse();
            },
            cancellationToken: cancellationToken);
    }
}
