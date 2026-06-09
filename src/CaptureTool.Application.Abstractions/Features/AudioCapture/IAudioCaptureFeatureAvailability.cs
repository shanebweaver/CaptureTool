namespace CaptureTool.Application.Abstractions.Features.AudioCapture;

public interface IAudioCaptureFeatureAvailability
{
    bool IsAudioCaptureEnabled { get; }
}
