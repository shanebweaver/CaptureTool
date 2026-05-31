namespace CaptureTool.Infrastructure.Abstractions.Audio;

public enum AudioInputSourcesChangeReason
{
    EnumerationCompleted,
    Added,
    Removed,
    Updated,
    Stopped
}
