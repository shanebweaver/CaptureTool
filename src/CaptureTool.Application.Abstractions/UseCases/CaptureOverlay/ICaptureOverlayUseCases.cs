namespace CaptureTool.Application.Abstractions.UseCases.CaptureOverlay;

public interface ICaptureOverlayUseCases
{
    bool CanClose();
    void Close();
    bool CanGoBack();
    void GoBack();
    bool CanToggleDesktopAudio();
    void ToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(CaptureTool.Domain.Capture.Abstractions.NewCaptureArgs args);
    void StartVideoCapture(CaptureTool.Domain.Capture.Abstractions.NewCaptureArgs args);
    // Stop video capture
    bool CanStopVideoCapture();
    void StopVideoCapture();
    void TogglePauseResume();
}