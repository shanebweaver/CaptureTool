using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayActions : ICaptureOverlayActions
{
    private readonly ICaptureOverlayCloseAction _closeAction;
    private readonly ICaptureOverlayGoBackAction _goBackAction;
    private readonly ICaptureOverlayToggleDesktopAudioAction _toggleDesktopAudioAction;

    public CaptureOverlayActions(
        ICaptureOverlayCloseAction closeAction,
        ICaptureOverlayGoBackAction goBackAction,
        ICaptureOverlayToggleDesktopAudioAction toggleDesktopAudioAction)
    {
        _closeAction = closeAction;
        _goBackAction = goBackAction;
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
