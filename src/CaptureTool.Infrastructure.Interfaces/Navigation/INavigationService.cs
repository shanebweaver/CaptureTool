namespace CaptureTool.Infrastructure.Interfaces.Navigation;

public interface INavigationService
{
    event EventHandler<INavigationEventArgs> Navigated;
    INavigationRequest? CurrentRequest { get; }
    bool CanGoBack { get; }
    void SetNavigationHandler(INavigationHandler handler);
    void Navigate(object route, object? parameter = null, bool clearHistory = false);
    bool TryGoBack();
    bool TryGoBackTo(Func<INavigationRequest, bool> assessRequest);
}
