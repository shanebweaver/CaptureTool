using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Services.Implementations.Navigation;

public sealed partial class NavigationEventArgs(NavigationRequest request) : INavigationEventArgs
{
    public INavigationRequest Request { get; } = request;
}