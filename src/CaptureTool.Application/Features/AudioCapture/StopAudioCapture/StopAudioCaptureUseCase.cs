using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.StopAudioCapture;

public sealed class StopAudioCaptureUseCase : IStopAudioCaptureUseCase
{
    private const string ActivityId = "StopAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StopAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<UseCaseResponse<StopAudioCaptureResponse>> ExecuteAsync(StopAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureHandler.StopCapture();
                return new StopAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
