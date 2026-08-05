namespace CaptureTool.Application.Abstractions.Navigation;

public interface INavigationHandler
{
    Task<NavigationResult> HandleNavigationRequestAsync(
        INavigationRequest request,
        CancellationToken cancellationToken = default);
}
