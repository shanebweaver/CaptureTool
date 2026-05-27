namespace CaptureTool.Application.Navigation;

public static partial class CaptureToolNavigationRouteHelper
{
    public static bool IsMainWindowRoute(object route)
    {
        if (route is NavigationRoute navigationRoute)
        {
            return
                navigationRoute == NavigationRoute.Home ||
                navigationRoute == NavigationRoute.Loading ||
                navigationRoute == NavigationRoute.Store ||
                navigationRoute == NavigationRoute.Error ||
                navigationRoute == NavigationRoute.About ||
                navigationRoute == NavigationRoute.Settings ||
                navigationRoute == NavigationRoute.ImageEdit ||
                navigationRoute == NavigationRoute.VideoEdit ||
                navigationRoute == NavigationRoute.AudioCapture ||
                navigationRoute == NavigationRoute.AudioEdit;
        }

        return false;
    }

    public static bool IsOverlayRoute(object route)
    {
        if (route is NavigationRoute navigationRoute)
        {
            return
                navigationRoute == NavigationRoute.SelectionOverlay ||
                navigationRoute == NavigationRoute.CaptureOverlay;
        }

        return false;
    }
}