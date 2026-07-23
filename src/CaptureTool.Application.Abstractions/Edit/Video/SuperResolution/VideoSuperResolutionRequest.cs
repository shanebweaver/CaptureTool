using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;

public sealed record VideoSuperResolutionRequest(
    VideoFile SourceVideo,
    double ScaleFactor = 2.0);
