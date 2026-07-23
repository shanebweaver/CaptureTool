using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;

public sealed record VideoSuperResolutionResult(
    VideoSuperResolutionStatus Status,
    VideoFile? VideoFile = null,
    string? ErrorMessage = null)
{
    public static VideoSuperResolutionResult Success(VideoFile videoFile)
    {
        return new(VideoSuperResolutionStatus.Success, videoFile);
    }

    public static VideoSuperResolutionResult Cancelled { get; } =
        new(VideoSuperResolutionStatus.Cancelled);

    public static VideoSuperResolutionResult NotSupported { get; } =
        new(VideoSuperResolutionStatus.NotSupported);

    public static VideoSuperResolutionResult NotReady { get; } =
        new(VideoSuperResolutionStatus.NotReady);

    public static VideoSuperResolutionResult UnsupportedVideo(string? errorMessage = null)
    {
        return new(VideoSuperResolutionStatus.UnsupportedVideo, ErrorMessage: errorMessage);
    }

    public static VideoSuperResolutionResult Failed(string? errorMessage = null)
    {
        return new(VideoSuperResolutionStatus.Failed, ErrorMessage: errorMessage);
    }
}
