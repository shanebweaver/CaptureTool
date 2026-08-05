using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Infrastructure.Tests.Navigation;

public class MockNavigationHandler : INavigationHandler
{
    public List<INavigationRequest> HandledRequests { get; } = [];

    public NavigationResult Result { get; set; } = NavigationResult.Accepted;

    public Func<INavigationRequest, CancellationToken, Task<NavigationResult>>? HandleAsync { get; set; }

    public Task<NavigationResult> HandleNavigationRequestAsync(
        INavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        HandledRequests.Add(request);
        return HandleAsync?.Invoke(request, cancellationToken) ?? Task.FromResult(Result);
    }
}
