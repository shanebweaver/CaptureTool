using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Capture.Audio;

internal interface IAudioCaptureWorkflow : IAudioCaptureState
{
    void StartCapture();
    AudioFile StopCapture();
    void CancelCapture();
    void PauseCapture();
    void SelectAudioInputSource(string? sourceId);
    void ToggleLocalAudio();
    void ToggleMute();
}
