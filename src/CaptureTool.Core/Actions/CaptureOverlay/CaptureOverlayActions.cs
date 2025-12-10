namespace CaptureTool.Core.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayActions
{
    private readonly CaptureOverlayCloseAction _closeAction;
    private readonly CaptureOverlayGoBackAction _goBackAction;
    private readonly CaptureOverlayToggleDesktopAudioAction _toggleDesktopAudioAction;

    public CaptureOverlayActions(
        CaptureOverlayCloseAction closeAction,
        CaptureOverlayGoBackAction goBackCommand,
        CaptureOverlayToggleDesktopAudioAction toggleDesktopAudioAction)
    {
        _closeAction = closeAction;
        _goBackAction = goBackCommand;
        _toggleDesktopAudioAction = toggleDesktopAudioAction;
    }

    // Close
    public bool CanClose() => _closeAction.CanExecute();
    public void Close() => _closeAction.ExecuteCommand();

    // Go back
    public bool CanGoBack() => _goBackAction.CanExecute();
    public void GoBack() => _goBackAction.ExecuteCommand();

    // Toggle desktop audio
    public bool CanToggleDesktopAudio() => _toggleDesktopAudioAction.CanExecute();
    public void ToggleDesktopAudio() => _toggleDesktopAudioAction.ExecuteCommand();
}
