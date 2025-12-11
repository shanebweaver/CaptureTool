using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayActions : ICaptureOverlayActions
{
    private readonly ICaptureOverlayCloseAction _closeAction;
    private readonly ICaptureOverlayGoBackAction _goBackAction;
    private readonly ICaptureOverlayToggleDesktopAudioAction _toggleDesktopAudioAction;
    private readonly ICaptureOverlayStartVideoCaptureAction _startVideoCaptureAction;

    public CaptureOverlayActions(
        ICaptureOverlayCloseAction closeAction,
        ICaptureOverlayGoBackAction goBackAction,
        ICaptureOverlayToggleDesktopAudioAction toggleDesktopAudioAction,
        ICaptureOverlayStartVideoCaptureAction startVideoCaptureAction)
    {
        _closeAction = closeAction;
        _goBackAction = goBackAction;
        _toggleDesktopAudioAction = toggleDesktopAudioAction;
        _startVideoCaptureAction = startVideoCaptureAction;
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

    // Start video capture
    public bool CanStartVideoCapture(NewCaptureArgs args) => _startVideoCaptureAction.CanExecute(args);
    public void StartVideoCapture(NewCaptureArgs args) => _startVideoCaptureAction.ExecuteCommand(args);
}
