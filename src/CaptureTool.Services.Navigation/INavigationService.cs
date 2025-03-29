namespace CaptureTool.Services.Navigation;

public interface INavigationService
{
    bool CanGoBack { get; }
    void SetNavigationHandler(INavigationHandler handler);
    void Navigate(string key, object? parameter = null);
    void GoBack();
    void ClearNavigationHistory();
}