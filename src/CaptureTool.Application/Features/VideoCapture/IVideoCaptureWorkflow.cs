using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.VideoCapture;

internal interface IVideoCaptureWorkflow : IVideoCaptureState
{
    void PrepareForVideoCapture();
    void StartVideoCapture(NewCaptureArgs args);
    PendingVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void SetIsDesktopAudioEnabled(bool value);
    void SetIsAudioInputMuted(bool value);
    void SelectAudioInputSource(string? sourceId);
    void SetAudioInputVolume(int volumePercentage);
    void ToggleDesktopAudioCapture(bool enabled);
    void ToggleIsPaused(bool isPaused);
}
