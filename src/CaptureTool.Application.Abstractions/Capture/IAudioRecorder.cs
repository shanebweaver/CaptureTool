using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IAudioRecorder
{
    event EventHandler<AudioCaptureLevel>? AudioLevelCaptured;

    void Pause();
    void Resume();
    void StartCapture(AudioCaptureRecordingOptions options);
    AudioFile StopCapture();
    void SetDesktopAudioEnabled(bool enabled);
    void SetAudioInputSource(string? sourceId);
}

public readonly record struct AudioCaptureRecordingOptions(
    string OutputPath,
    bool CaptureDesktopAudio,
    string? AudioInputSourceId = null,
    int AudioInputVolumePercentage = 100);
