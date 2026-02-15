namespace CaptureTool.Application.Interfaces.UseCases.AppMenu;

public interface IAppMenuUseCases
{
    Task<IEnumerable<IRecentCapture>> LoadRecentCapturesAsync(CancellationToken ct);
    Task OpenRecentCaptureAsync(string filePath, CancellationToken ct);
    Task OpenFileAsync(CancellationToken ct);
    void NewImageCapture();
    void NewVideoCapture();
    void NavigateToSettings();
    void ShowAboutApp();
    void ShowAddOns();
    void ExitApplication();
}
