using CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Image.CaptureAllScreensImage;

internal sealed class CaptureAllScreensImageUseCase : ICaptureAllScreensImageUseCase
{
    private const string ActivityId = "CaptureAllScreensImage";

    private readonly IImageCaptureWorkflow _imageCaptureWorkflow;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public CaptureAllScreensImageUseCase(
        IImageCaptureWorkflow imageCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _imageCaptureWorkflow = imageCaptureWorkflow;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<CaptureAllScreensImageResponse>> ExecuteAsync(
        CaptureAllScreensImageRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () => new CaptureAllScreensImageResponse(_imageCaptureWorkflow.CaptureMonitors(request.Monitors)),
            cancellationToken: cancellationToken);
    }
}
