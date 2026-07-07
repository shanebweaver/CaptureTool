using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;

internal sealed class ToggleLocalAudioCaptureUseCase : IToggleLocalAudioCaptureUseCase
{
    private const string ActivityId = "ToggleLocalAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;

    public ToggleLocalAudioCaptureUseCase(IAudioCaptureWorkflow audioCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
    }

    public Task<UseCaseResponse<ToggleLocalAudioCaptureResponse>> ExecuteAsync(ToggleLocalAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureWorkflow.ToggleLocalAudio();
                return new ToggleLocalAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
