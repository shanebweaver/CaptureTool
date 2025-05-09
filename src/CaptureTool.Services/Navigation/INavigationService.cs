using System;

namespace CaptureTool.Services.Navigation;

public interface INavigationService
{
    event EventHandler<NavigationEventArgs> Navigated;
    bool CanGoBack { get; }
    NavigationRoute CurrentRoute { get; }
    void SetNavigationHandler(INavigationHandler handler);
    void Navigate(NavigationRoute route, object? parameter = null, bool clearHistory = false);
    void GoBack();
}
