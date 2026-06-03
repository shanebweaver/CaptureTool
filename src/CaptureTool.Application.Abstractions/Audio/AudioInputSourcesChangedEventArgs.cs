namespace CaptureTool.Application.Abstractions.Audio;

public sealed class AudioInputSourcesChangedEventArgs : EventArgs
{
    public AudioInputSourcesChangedEventArgs(
        AudioInputSourcesChangeReason reason,
        IReadOnlyList<AudioInputSource> sources,
        string? affectedSourceId = null)
    {
        Reason = reason;
        Sources = sources;
        AffectedSourceId = affectedSourceId;
    }

    public AudioInputSourcesChangeReason Reason { get; }
    public IReadOnlyList<AudioInputSource> Sources { get; }
    public string? AffectedSourceId { get; }
}
