namespace CaptureTool.Presentation.Features.Media;

public enum MediaLoadState
{
    Loading,
    Ready,
    Failed
}

public enum MediaFailureCategory
{
    Finalization,
    FileUnavailable,
    Unsupported,
    Playback
}
