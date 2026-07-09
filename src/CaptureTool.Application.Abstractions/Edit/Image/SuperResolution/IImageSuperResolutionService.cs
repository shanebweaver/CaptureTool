namespace CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;

public interface IImageSuperResolutionService
{
    ImageSuperResolutionReadyState GetReadyState();

    Task<ImageSuperResolutionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<ImageSuperResolutionResult> GenerateAsync(
        ImageSuperResolutionRequest request,
        CancellationToken cancellationToken = default);
}
