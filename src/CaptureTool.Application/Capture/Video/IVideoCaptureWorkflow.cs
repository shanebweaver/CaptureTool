using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal interface IVideoCaptureWorkflow : IVideoCaptureState
{
    void PrepareForVideoCapture();
    void StartVideoCapture(NewCaptureArgs args);
    PendingVideoFile StopVideoCapture();
    void CancelVideoCapture(CancelVideoCaptureReason reason = CancelVideoCaptureReason.User);
    void SetIsDesktopAudioEnabled(bool value);
    void SetDesktopAudioVolume(int volumePercentage);
    void SetIsAudioInputMuted(bool value);
    void SelectAudioInputSource(string? sourceId);
    void SetAudioInputVolume(int volumePercentage);
    void ToggleIsPaused(bool isPaused);
}
