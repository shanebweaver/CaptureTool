using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;

public sealed class OpenSelectionOverlayUseCase : IOpenSelectionOverlayUseCase
{
    private readonly INavigationService _navigationService;

    public OpenSelectionOverlayUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenSelectionOverlayResponse> ExecuteAsync(OpenSelectionOverlayRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.SelectionOverlay, request.CaptureOptions);
        return Task.FromResult(new OpenSelectionOverlayResponse());
    }
}