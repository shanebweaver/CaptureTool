using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayActions : ICaptureOverlayActions
{
    private readonly ICaptureOverlayCloseAction _closeAction;
    private readonly ICaptureOverlayGoBackAction _goBackAction;
    private readonly ICaptureOverlayToggleDesktopAudioAction _toggleDesktopAudioAction;
    private readonly ICaptureOverlayTogglePauseResumeAction _togglePauseResumeAction;
    private readonly ICaptureOverlayStartVideoCaptureAction _startVideoCaptureAction;
    private readonly ICaptureOverlayStopVideoCaptureAction _stopVideoCaptureAction;

    public CaptureOverlayActions(
        ICaptureOverlayCloseAction closeAction,
        ICaptureOverlayGoBackAction goBackAction,
        ICaptureOverlayToggleDesktopAudioAction toggleDesktopAudioAction,
        ICaptureOverlayTogglePauseResumeAction togglePauseResumeAction,
        ICaptureOverlayStartVideoCaptureAction startVideoCaptureAction,
        ICaptureOverlayStopVideoCaptureAction stopVideoCaptureAction)
    {
        _closeAction = closeAction;
        _goBackAction = goBackAction;
        _toggleDesktopAudioAction = toggleDesktopAudioAction;
        _startVideoCaptureAction = startVideoCaptureAction;
        _stopVideoCaptureAction = stopVideoCaptureAction;
        _togglePauseResumeAction = togglePauseResumeAction;
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

    // Stop video capture
    public bool CanStopVideoCapture() => _stopVideoCaptureAction.CanExecute();
    public void StopVideoCapture() => _stopVideoCaptureAction.ExecuteCommand();

    // Toggle pause/resume
    public bool CanTogglePauseResume() => _togglePauseResumeAction.CanExecute();
    public void TogglePauseResume() => _togglePauseResumeAction.ExecuteCommand();
}
