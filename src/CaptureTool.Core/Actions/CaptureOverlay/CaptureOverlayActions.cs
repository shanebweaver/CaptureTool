namespace CaptureTool.Core.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayActions
{
    private readonly CaptureOverlayGoBackAction _goBackCommand;

    public CaptureOverlayActions(
        CaptureOverlayGoBackAction goBackCommand)
    {
        _goBackCommand = goBackCommand;
    }

    public bool CanGoBack() => _goBackCommand.CanExecute();

    public void GoBack() => _goBackCommand.ExecuteCommand();
}
