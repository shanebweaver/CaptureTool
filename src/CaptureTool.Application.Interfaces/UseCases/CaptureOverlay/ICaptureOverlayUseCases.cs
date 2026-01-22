namespace CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;

public interface ICaptureOverlayUseCases
{
    bool CanClose();
    void Close();
    bool CanGoBack();
    void GoBack();
    bool CanToggleDesktopAudio();
    void ToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(CaptureTool.Domain.Capture.Interfaces.NewCaptureArgs args);
    void StartVideoCapture(CaptureTool.Domain.Capture.Interfaces.NewCaptureArgs args);
    // Stop video capture
    bool CanStopVideoCapture();
    void StopVideoCapture();
    void TogglePauseResume();
}