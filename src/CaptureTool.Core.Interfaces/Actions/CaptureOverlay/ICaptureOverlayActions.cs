namespace CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayActions
{
    bool CanClose();
    bool CanGoBack();
    bool CanToggleDesktopAudio();
    void Close();
    void GoBack();
    void ToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
    void StartVideoCapture(CaptureTool.Domains.Capture.Interfaces.NewCaptureArgs args);
}