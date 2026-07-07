namespace CaptureTool.Application.Capture.Audio;

internal readonly record struct AudioCaptureSettings(
    bool IsMuted,
    bool IsDesktopAudioEnabled,
    string? SelectedAudioInputSourceId)
{
    public static AudioCaptureSettings Default { get; } = new(
        IsMuted: false,
        IsDesktopAudioEnabled: true,
        SelectedAudioInputSourceId: null);

    public AudioCaptureSettings WithMuted(bool isMuted)
        => this with { IsMuted = isMuted };

    public AudioCaptureSettings WithDesktopAudioEnabled(bool isDesktopAudioEnabled)
        => this with { IsDesktopAudioEnabled = isDesktopAudioEnabled };

    public AudioCaptureSettings WithAudioInputSource(string? sourceId)
        => this with
        {
            SelectedAudioInputSourceId = string.IsNullOrWhiteSpace(sourceId)
                ? null
                : sourceId
        };
}
