using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.StopAudioCapture;

public sealed class StopAudioCaptureUseCase : IStopAudioCaptureUseCase
{
    private const string ActivityId = "StopAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;
    private readonly INavigationService _navigationService;

    public StopAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<StopAudioCaptureResponse>> ExecuteAsync(StopAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var audioFile = _audioCaptureHandler.StopCapture();
                _navigationService.Navigate(NavigationRoute.AudioEdit, audioFile);
                return new StopAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
