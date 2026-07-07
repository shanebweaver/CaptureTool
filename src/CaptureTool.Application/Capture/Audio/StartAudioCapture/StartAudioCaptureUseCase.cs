using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.StartAudioCapture;

internal sealed class StartAudioCaptureUseCase : IStartAudioCaptureUseCase
{
    private const string ActivityId = "StartAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;

    public StartAudioCaptureUseCase(IAudioCaptureWorkflow audioCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
    }

    public Task<UseCaseResponse<StartAudioCaptureResponse>> ExecuteAsync(StartAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureWorkflow.StartCapture();
                return new StartAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
