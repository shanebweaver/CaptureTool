using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.OpenAudioCapturePage;

internal sealed class OpenAudioCapturePageUseCase : IOpenAudioCapturePageUseCase
{
    private const string ActivityId = "OpenAudioCapturePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenAudioCapturePageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard audioCaptureNavigationGuard)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard;
    }

    public Task<UseCaseResponse<OpenAudioCapturePageResponse>> ExecuteAsync(OpenAudioCapturePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenAudioCapturePageResponse(false);
                }

                _navigationService.Navigate(NavigationRoute.AudioCapture);
                return new OpenAudioCapturePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
