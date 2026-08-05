namespace CaptureTool.Application.Abstractions.Navigation;

public interface INavigationService
{
    event EventHandler<INavigationEventArgs> Navigated;
    INavigationRequest? CurrentRequest { get; }
    bool CanGoBack { get; }
    void SetNavigationHandler(INavigationHandler handler);
    Task<NavigationResult> NavigateAsync(
        object route,
        object? parameter = null,
        bool clearHistory = false,
        CancellationToken cancellationToken = default);
    Task<NavigationResult> TryGoBackAsync(CancellationToken cancellationToken = default);
    Task<NavigationResult> TryGoBackToAsync(
        Func<INavigationRequest, bool> assessRequest,
        CancellationToken cancellationToken = default);
}
