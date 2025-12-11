namespace CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayActions
{
    bool CanClose();
    bool CanGoBack();
    bool CanToggleDesktopAudio();
    void Close();
    void GoBack();
    void ToggleDesktopAudio();
}