using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SetVideoCaptureAudioInputMuted;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.SetVideoCaptureAudioInputMuted;

internal sealed class SetVideoCaptureAudioInputMutedUseCase : ISetVideoCaptureAudioInputMutedUseCase
{
    private const string ActivityId = "SetVideoCaptureAudioInputMuted";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public SetVideoCaptureAudioInputMutedUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public Task<UseCaseResponse<SetVideoCaptureAudioInputMutedResponse>> ExecuteAsync(SetVideoCaptureAudioInputMutedRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _videoCaptureWorkflow.SetIsAudioInputMuted(request.IsMuted);
                return new SetVideoCaptureAudioInputMutedResponse();
            },
            cancellationToken: cancellationToken);
    }
}
