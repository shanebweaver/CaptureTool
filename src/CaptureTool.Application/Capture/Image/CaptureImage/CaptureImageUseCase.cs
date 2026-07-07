using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Image.CaptureImage;

internal sealed class CaptureImageUseCase : ICaptureImageUseCase
{
    private const string ActivityId = "CaptureImage";

    private readonly IImageCaptureWorkflow _imageCaptureWorkflow;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public CaptureImageUseCase(
        IImageCaptureWorkflow imageCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _imageCaptureWorkflow = imageCaptureWorkflow;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<CaptureImageResponse>> ExecuteAsync(
        CaptureImageRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () => new CaptureImageResponse(_imageCaptureWorkflow.CaptureImage(request.CaptureArgs)),
            cancellationToken: cancellationToken);
    }
}
