namespace CaptureTool.Application.Implementations.Services.Navigation;

public static partial class CaptureToolNavigationRouteHelper
{
    public static bool IsMainWindowRoute(object route)
    {
        if (route is CaptureToolNavigationRoute navigationRoute)
        {
            return
                navigationRoute == CaptureToolNavigationRoute.Home ||
                navigationRoute == CaptureToolNavigationRoute.Loading ||
                navigationRoute == CaptureToolNavigationRoute.AddOns ||
                navigationRoute == CaptureToolNavigationRoute.Error ||
                navigationRoute == CaptureToolNavigationRoute.About ||
                navigationRoute == CaptureToolNavigationRoute.Settings ||
                navigationRoute == CaptureToolNavigationRoute.ImageEdit ||
                navigationRoute == CaptureToolNavigationRoute.VideoEdit;
        }

        return false;
    }

    public static bool IsOverlayRoute(object route)
    {
        if (route is CaptureToolNavigationRoute navigationRoute)
        {
            return
                navigationRoute == CaptureToolNavigationRoute.ImageCapture ||
                navigationRoute == CaptureToolNavigationRoute.VideoCapture;
        }

        return false;
    }
}