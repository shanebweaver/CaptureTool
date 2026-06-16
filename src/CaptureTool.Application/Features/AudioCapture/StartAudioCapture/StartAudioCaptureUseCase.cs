using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.StartAudioCapture;

public sealed class StartAudioCaptureUseCase : IStartAudioCaptureUseCase
{
    private const string ActivityId = "StartAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StartAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<UseCaseResponse<StartAudioCaptureResponse>> ExecuteAsync(StartAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureHandler.StartCapture();
                return new StartAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
