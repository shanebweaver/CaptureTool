using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.CancelVideoCapture;

internal sealed class CancelVideoCaptureUseCase : ICancelVideoCaptureUseCase
{
    private const string ActivityId = "CancelVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public CancelVideoCaptureUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public Task<UseCaseResponse<CancelVideoCaptureResponse>> ExecuteAsync(CancelVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _videoCaptureWorkflow.CancelVideoCapture();
                return new CancelVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
