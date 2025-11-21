using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Services.Tests.Navigation;

public class MockNavigationHandler : INavigationHandler
{
    public List<INavigationRequest> HandledRequests { get; } = new();

    public void HandleNavigationRequest(INavigationRequest request)
    {
        HandledRequests.Add(request);
    }
}
