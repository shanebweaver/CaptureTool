using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;

internal sealed class PauseAudioCaptureUseCase : IPauseAudioCaptureUseCase
{
    private const string ActivityId = "PauseAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;

    public PauseAudioCaptureUseCase(IAudioCaptureWorkflow audioCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
    }

    public Task<UseCaseResponse<PauseAudioCaptureResponse>> ExecuteAsync(PauseAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureWorkflow.PauseCapture();
                return new PauseAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
