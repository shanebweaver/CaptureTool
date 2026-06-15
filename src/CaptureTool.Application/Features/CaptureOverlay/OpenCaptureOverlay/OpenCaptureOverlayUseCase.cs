using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;

public sealed class OpenCaptureOverlayUseCase : IOpenCaptureOverlayUseCase
{
    private readonly INavigationService _navigationService;

    public OpenCaptureOverlayUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenCaptureOverlayResponse> ExecuteAsync(OpenCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
            return Task.FromResult(new OpenCaptureOverlayResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new OpenCaptureOverlayResponse(false));
        }
    }
}
