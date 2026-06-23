using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;

public sealed class OpenVideoEditPageUseCase : IOpenVideoEditPageUseCase
{
    private const string ActivityId = "OpenVideoEditPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenVideoEditPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard ?? new AllowAudioCaptureNavigationGuard();
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<OpenVideoEditPageResponse>> ExecuteAsync(OpenVideoEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenVideoEditPageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.VideoEdit, request.VideoFile);
                return new OpenVideoEditPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
