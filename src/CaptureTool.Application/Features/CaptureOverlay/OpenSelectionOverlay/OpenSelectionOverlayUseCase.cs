using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;

public sealed class OpenSelectionOverlayUseCase : IOpenSelectionOverlayUseCase
{
    private const string ActivityId = "OpenSelectionOverlay";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenSelectionOverlayUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard ?? new AllowAudioCaptureNavigationGuard();
    }

    public Task<UseCaseResponse<OpenSelectionOverlayResponse>> ExecuteAsync(OpenSelectionOverlayRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenSelectionOverlayResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.SelectionOverlay, request.CaptureOptions);
                return new OpenSelectionOverlayResponse();
            },
            cancellationToken: cancellationToken);
    }
}
