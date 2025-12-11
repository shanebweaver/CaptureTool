using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayActions
{
    bool CanClose();
    bool CanGoBack();
    bool CanToggleDesktopAudio();
    // Start video capture
    bool CanStartVideoCapture(NewCaptureArgs args);
    void StartVideoCapture(NewCaptureArgs args);
    // Stop video capture
    bool CanStopVideoCapture();
    void StopVideoCapture();
    void Close();
    void GoBack();
    void ToggleDesktopAudio();
}