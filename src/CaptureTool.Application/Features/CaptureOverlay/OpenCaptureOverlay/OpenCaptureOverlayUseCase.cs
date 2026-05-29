using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;

public sealed class OpenCaptureOverlayUseCase : IUseCase<OpenCaptureOverlayRequest, OpenCaptureOverlayResponse>
{
    private readonly INavigationService _navigationService;

    public OpenCaptureOverlayUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenCaptureOverlayResponse> ExecuteAsync(OpenCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
        return Task.FromResult(new OpenCaptureOverlayResponse());
    }
}