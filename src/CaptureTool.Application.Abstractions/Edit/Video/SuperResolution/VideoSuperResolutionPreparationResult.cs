namespace CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;

public sealed record VideoSuperResolutionPreparationResult(
    VideoSuperResolutionPreparationStatus Status,
    string? ErrorMessage = null)
{
    public static VideoSuperResolutionPreparationResult Success { get; } =
        new(VideoSuperResolutionPreparationStatus.Success);

    public static VideoSuperResolutionPreparationResult Cancelled { get; } =
        new(VideoSuperResolutionPreparationStatus.Cancelled);

    public static VideoSuperResolutionPreparationResult NotSupported { get; } =
        new(VideoSuperResolutionPreparationStatus.NotSupported);

    public static VideoSuperResolutionPreparationResult Failed(string? errorMessage = null)
    {
        return new(VideoSuperResolutionPreparationStatus.Failed, errorMessage);
    }
}
