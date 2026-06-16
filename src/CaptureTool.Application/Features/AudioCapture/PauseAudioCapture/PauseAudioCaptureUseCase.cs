using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;

public sealed class PauseAudioCaptureUseCase : IPauseAudioCaptureUseCase
{
    private const string ActivityId = "PauseAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public PauseAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<UseCaseResponse<PauseAudioCaptureResponse>> ExecuteAsync(PauseAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureHandler.PauseCapture();
                return new PauseAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
