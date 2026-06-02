using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Infrastructure.Navigation;

public sealed partial class NavigationEventArgs(NavigationRequest request) : INavigationEventArgs
{
    public INavigationRequest Request { get; } = request;
}