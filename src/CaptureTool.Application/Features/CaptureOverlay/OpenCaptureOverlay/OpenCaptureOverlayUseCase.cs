using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenCaptureOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenCaptureOverlay;

public sealed class OpenCaptureOverlayUseCase : IOpenCaptureOverlayUseCase
{
    private const string ActivityId = "OpenCaptureOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenCaptureOverlayUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard ?? new AllowAudioCaptureNavigationGuard();
    }

    public Task<UseCaseResponse<OpenCaptureOverlayResponse>> ExecuteAsync(OpenCaptureOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenCaptureOverlayResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.CaptureOverlay, request.CaptureArgs);
                return new OpenCaptureOverlayResponse();
            },
            cancellationToken: cancellationToken);
    }
}
