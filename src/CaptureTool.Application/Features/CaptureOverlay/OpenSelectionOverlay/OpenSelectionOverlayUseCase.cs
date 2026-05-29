using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;

public sealed class OpenSelectionOverlayUseCase : IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse>
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