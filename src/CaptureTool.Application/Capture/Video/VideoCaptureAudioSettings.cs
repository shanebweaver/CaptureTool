namespace CaptureTool.Application.Capture.Video;

internal readonly record struct VideoCaptureAudioSettings(
    bool IsDesktopAudioEnabled,
    bool IsAudioInputMuted,
    int AudioInputVolumePercentage,
    string? SelectedAudioInputSourceId)
{
    public static VideoCaptureAudioSettings Default { get; } = new(
        IsDesktopAudioEnabled: false,
        IsAudioInputMuted: false,
        AudioInputVolumePercentage: 100,
        SelectedAudioInputSourceId: null);

    public bool ShouldCaptureAudio
        => !IsAudioInputMuted && (IsDesktopAudioEnabled || !string.IsNullOrWhiteSpace(SelectedAudioInputSourceId));

    public VideoCaptureAudioSettings PrepareForCapture(bool defaultDesktopAudioEnabled)
        => this with
        {
            IsDesktopAudioEnabled = defaultDesktopAudioEnabled,
            IsAudioInputMuted = false,
            AudioInputVolumePercentage = 100
        };

    public VideoCaptureAudioSettings WithDesktopAudioEnabled(bool value)
        => this with { IsDesktopAudioEnabled = value };

    public VideoCaptureAudioSettings WithAudioInputMuted(bool value)
        => this with { IsAudioInputMuted = value };

    public VideoCaptureAudioSettings WithAudioInputSource(string? sourceId)
        => this with
        {
            SelectedAudioInputSourceId = string.IsNullOrWhiteSpace(sourceId)
                ? null
                : sourceId
        };

    public VideoCaptureAudioSettings WithAudioInputVolume(int volumePercentage)
        => this with { AudioInputVolumePercentage = Math.Clamp(volumePercentage, 0, 100) };
}
