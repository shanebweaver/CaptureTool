namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public interface IImageDescriptionService
{
    ImageDescriptionReadyState GetReadyState();

    Task<ImageDescriptionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<ImageDescriptionResult> DescribeAsync(
        ImageDescriptionRequest request,
        CancellationToken cancellationToken = default);
}
