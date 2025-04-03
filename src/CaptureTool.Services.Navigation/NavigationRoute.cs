namespace CaptureTool.Services.Navigation;

public sealed partial class NavigationRoute(string routeName)
{
    public string RouteName { get; } = routeName;
}
