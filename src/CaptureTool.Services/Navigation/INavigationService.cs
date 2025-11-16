using System;

namespace CaptureTool.Services.Navigation;

public interface INavigationService
{
    event EventHandler<NavigationEventArgs> Navigated;
    NavigationRequest? CurrentRequest { get; }
    bool CanGoBack { get; }
    void SetNavigationHandler(INavigationHandler handler);
    void Navigate(NavigationRoute route, object? parameter = null, bool clearHistory = false);
    bool TryGoBack();
    bool TryGoBackTo(Func<NavigationRequest, bool> assessRequest);
}
