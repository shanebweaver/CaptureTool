using CaptureTool.Application.Abstractions.Features.CaptureOverlay.PrepareVideoCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.PrepareVideoCapture;

internal sealed class PrepareVideoCaptureUseCase : IPrepareVideoCaptureUseCase
{
    private const string ActivityId = "PrepareVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public PrepareVideoCaptureUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public Task<UseCaseResponse<PrepareVideoCaptureResponse>> ExecuteAsync(PrepareVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _videoCaptureWorkflow.PrepareForVideoCapture();
                return new PrepareVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
