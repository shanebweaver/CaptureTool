using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Infrastructure.Implementations.Navigation;

public sealed partial class NavigationEventArgs(NavigationRequest request) : INavigationEventArgs
{
    public INavigationRequest Request { get; } = request;
}