using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Infrastructure.Implementations.Navigation;

public readonly struct NavigationRequest : INavigationRequest
{
    public object Route { get; }
    public object? Parameter { get; }
    public bool IsBackNavigation { get; }
    public bool ClearHistory { get; }

    public NavigationRequest(
        object route, 
        object? parameter = null, 
        bool isBackNavigation = false, 
        bool clearHistroy = false)
    {
        Route = route;
        Parameter = parameter;
        IsBackNavigation = isBackNavigation;
        ClearHistory = clearHistroy;
    }
}