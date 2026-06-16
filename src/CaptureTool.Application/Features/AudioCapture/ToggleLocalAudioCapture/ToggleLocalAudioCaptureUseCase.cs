using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;

public sealed class ToggleLocalAudioCaptureUseCase : IToggleLocalAudioCaptureUseCase
{
    private const string ActivityId = "ToggleLocalAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public ToggleLocalAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<UseCaseResponse<ToggleLocalAudioCaptureResponse>> ExecuteAsync(ToggleLocalAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureHandler.ToggleLocalAudio();
                return new ToggleLocalAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
