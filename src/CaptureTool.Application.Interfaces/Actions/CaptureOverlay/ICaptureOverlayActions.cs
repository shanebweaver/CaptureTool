namespace CaptureTool.Application.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayActions
{
    bool CanClose();
    void Close();
    bool CanGoBack();
    void GoBack();
    bool CanToggleDesktopAudio();
    void ToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
    void StartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
    // Stop video capture
    bool CanStopVideoCapture();
    void StopVideoCapture();
    void TogglePauseResume();
}