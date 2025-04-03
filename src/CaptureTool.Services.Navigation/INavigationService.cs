using System;

namespace CaptureTool.Services.Navigation;

public interface INavigationService
{
    event EventHandler<NavigationEventArgs> Navigated;
    bool CanGoBack { get; }
    void SetNavigationHandler(INavigationHandler handler);
    void Navigate(NavigationRoute route, object? parameter = null);
    void GoBack();
    void ClearNavigationHistory();
}
