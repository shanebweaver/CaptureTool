namespace CaptureTool.Application.Abstractions.Navigation;

public interface INavigationCoordinator
{
    INavigationRequest? CurrentRequest { get; }
    bool CanGoBack { get; }

    Task<bool> ExecuteTransitionAsync(
        Func<CancellationToken, Task<bool>> transition,
        CancellationToken cancellationToken = default);

    Task<bool> NavigateAsync(
        object route,
        object? parameter = null,
        bool clearHistory = false,
        CancellationToken cancellationToken = default);

    Task<bool> TryGoBackAsync(CancellationToken cancellationToken = default);

    Task<bool> TryGoBackToAsync(
        Func<INavigationRequest, bool> assessRequest,
        CancellationToken cancellationToken = default);
}
