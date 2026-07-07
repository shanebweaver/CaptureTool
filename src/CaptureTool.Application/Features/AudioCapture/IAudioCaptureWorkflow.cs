using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Features.AudioCapture;

internal interface IAudioCaptureWorkflow : IAudioCaptureState
{
    void StartCapture();
    AudioFile StopCapture();
    void PauseCapture();
    void SelectAudioInputSource(string? sourceId);
    void ToggleLocalAudio();
    void ToggleMute();
}
