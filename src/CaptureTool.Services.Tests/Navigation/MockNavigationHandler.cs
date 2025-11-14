using CaptureTool.Services.Navigation;
using System.Collections.Generic;

namespace CaptureTool.Services.Tests.Navigation;

public class MockNavigationHandler : INavigationHandler
{
    public List<NavigationRequest> HandledRequests { get; } = new();

    public void HandleNavigationRequest(NavigationRequest request)
    {
        HandledRequests.Add(request);
    }
}
