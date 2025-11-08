namespace CaptureTool.Services.Navigation;

public sealed partial class NavigationRoute(string routeId)
{
    public string Id { get; } = routeId;
}
