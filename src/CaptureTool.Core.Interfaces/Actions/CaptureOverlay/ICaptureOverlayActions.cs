namespace CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayActions
{
    bool CanClose();
    bool CanGoBack();
    bool CanToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
    void StartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
    // Stop video capture
    bool CanStopVideoCapture();
    void StopVideoCapture();
    void Close();
    void GoBack();
    void ToggleDesktopAudio();
}