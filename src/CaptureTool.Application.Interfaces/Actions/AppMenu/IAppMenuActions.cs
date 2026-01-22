using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.Actions.AppMenu;

public interface IAppMenuActions
{
    Task<IEnumerable<IRecentCapture>> LoadRecentCapturesAsync(CancellationToken ct);
    Task OpenRecentCaptureAsync(string filePath, CancellationToken ct);
    Task OpenFileAsync(CancellationToken ct);
    void NewImageCapture();
    void NavigateToSettings();
    void ShowAboutApp();
    void ShowAddOns();
    void ExitApplication();
}
