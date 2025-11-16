namespace CaptureTool.Core.Navigation;

public static partial class CaptureToolNavigationRoutes
{
    public static bool IsMainWindowRoute(object route)
    {
        if (route is NavigationRoute navigationRoute)
        {
            return
                navigationRoute == NavigationRoute.Home ||
                navigationRoute == NavigationRoute.Loading ||
                navigationRoute == NavigationRoute.AddOns ||
                navigationRoute == NavigationRoute.Error ||
                navigationRoute == NavigationRoute.About ||
                navigationRoute == NavigationRoute.Settings ||
                navigationRoute == NavigationRoute.ImageEdit ||
                navigationRoute == NavigationRoute.VideoEdit;
        }

        return false;
    }
}