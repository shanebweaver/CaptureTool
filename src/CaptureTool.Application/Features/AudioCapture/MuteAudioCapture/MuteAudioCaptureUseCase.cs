using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;

internal sealed class MuteAudioCaptureUseCase : IMuteAudioCaptureUseCase
{
    private const string ActivityId = "MuteAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;

    public MuteAudioCaptureUseCase(IAudioCaptureWorkflow audioCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
    }

    public Task<UseCaseResponse<MuteAudioCaptureResponse>> ExecuteAsync(MuteAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureWorkflow.ToggleMute();
                return new MuteAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
