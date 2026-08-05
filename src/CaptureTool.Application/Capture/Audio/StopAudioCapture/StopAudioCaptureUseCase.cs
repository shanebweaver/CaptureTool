using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.StopAudioCapture;

internal sealed class StopAudioCaptureUseCase : IStopAudioCaptureUseCase
{
    private const string ActivityId = "StopAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;
    private readonly INavigationService _navigationService;

    public StopAudioCaptureUseCase(IAudioCaptureWorkflow audioCaptureWorkflow,
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<StopAudioCaptureResponse>> ExecuteAsync(StopAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var audioFile = _audioCaptureWorkflow.StopCapture();
                // This is the successful completion of the active audio workflow,
                // not an attempt to abandon it; the audio leave guard must not run.
                _navigationService.Navigate(NavigationRoute.AudioEdit, audioFile);
                return new StopAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
