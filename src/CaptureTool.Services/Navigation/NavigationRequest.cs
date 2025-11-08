namespace CaptureTool.Services.Navigation;

public readonly struct NavigationRequest
{
    public NavigationRoute Route { get; }
    public object? Parameter { get; }
    public bool IsBackNavigation { get; }
    public bool ClearHistory { get; }

    public NavigationRequest(
        NavigationRoute route, 
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