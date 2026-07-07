using CaptureTool.Application.Abstractions.Capture.Audio.SelectAudioCaptureInputSource;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.SelectAudioCaptureInputSource;

internal sealed class SelectAudioCaptureInputSourceUseCase : ISelectAudioCaptureInputSourceUseCase
{
    private const string ActivityId = "SelectAudioCaptureInputSource";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;

    public SelectAudioCaptureInputSourceUseCase(
        IAudioCaptureWorkflow audioCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
    }

    public Task<UseCaseResponse<SelectAudioCaptureInputSourceResponse>> ExecuteAsync(
        SelectAudioCaptureInputSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _audioCaptureWorkflow.SelectAudioInputSource(request.SourceId);
                return new SelectAudioCaptureInputSourceResponse();
            },
            cancellationToken: cancellationToken);
    }
}
