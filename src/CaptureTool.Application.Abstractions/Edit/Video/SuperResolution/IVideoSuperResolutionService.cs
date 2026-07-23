namespace CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;

public interface IVideoSuperResolutionService
{
    VideoSuperResolutionReadyState GetReadyState();

    Task<VideoSuperResolutionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default);

    Task<VideoSuperResolutionResult> GenerateAsync(
        VideoSuperResolutionRequest request,
        CancellationToken cancellationToken = default);
}
