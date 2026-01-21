using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Infrastructure.Tests.Navigation;

public class MockNavigationHandler : INavigationHandler
{
    public List<INavigationRequest> HandledRequests { get; } = new();

    public void HandleNavigationRequest(INavigationRequest request)
    {
        HandledRequests.Add(request);
    }
}
