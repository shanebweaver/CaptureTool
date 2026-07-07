namespace CaptureTool.Application.Abstractions.Capture.Audio;

public interface IAudioCaptureFeatureAvailability
{
    bool IsAudioCaptureEnabled { get; }
}
