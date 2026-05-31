namespace CaptureTool.Infrastructure.Abstractions.Audio;

public interface IAudioInputDetectionService
{
    event EventHandler<AudioInputSourcesChangedEventArgs>? AudioInputSourcesChanged;

    Task<IReadOnlyList<AudioInputSource>> GetAudioInputSourcesAsync(CancellationToken cancellationToken = default);
    void StartWatching();
    void StopWatching();
}
