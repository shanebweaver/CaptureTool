using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;

public sealed class MuteAudioCaptureUseCase : IMuteAudioCaptureUseCase
{
    private const string ActivityId = "MuteAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public MuteAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<UseCaseResponse<MuteAudioCaptureResponse>> ExecuteAsync(MuteAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureHandler.ToggleMute();
                return new MuteAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
