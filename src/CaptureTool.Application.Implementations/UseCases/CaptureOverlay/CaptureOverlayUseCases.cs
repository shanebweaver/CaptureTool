using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayUseCases : ICaptureOverlayUseCases
{
    private readonly ICaptureOverlayCloseUseCase _closeAction;
    private readonly ICaptureOverlayGoBackUseCase _goBackAction;
    private readonly ICaptureOverlayToggleDesktopAudioUseCase _toggleDesktopAudioAction;
    private readonly ICaptureOverlayTogglePauseResumeUseCase _togglePauseResumeAction;
    private readonly ICaptureOverlayStartVideoCaptureUseCase _startVideoCaptureAction;
    private readonly ICaptureOverlayStopVideoCaptureUseCase _stopVideoCaptureAction;

    public CaptureOverlayUseCases(
        ICaptureOverlayCloseUseCase closeAction,
        ICaptureOverlayGoBackUseCase goBackAction,
        ICaptureOverlayToggleDesktopAudioUseCase toggleDesktopAudioAction,
        ICaptureOverlayTogglePauseResumeUseCase togglePauseResumeAction,
        ICaptureOverlayStartVideoCaptureUseCase startVideoCaptureAction,
        ICaptureOverlayStopVideoCaptureUseCase stopVideoCaptureAction)
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
