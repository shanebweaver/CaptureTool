using CaptureTool.Services.Navigation;

namespace CaptureTool.Core;

public static partial class CaptureToolNavigationRoutes
{
    public static readonly NavigationRoute Loading = new("Loading");
    public static readonly NavigationRoute Error = new("Error");
    public static readonly NavigationRoute Home = new("Home");
    public static readonly NavigationRoute Settings = new("Settings");
    public static readonly NavigationRoute About = new("About");
    public static readonly NavigationRoute AddOns = new("AddOns");
    public static readonly NavigationRoute ImageCapture = new("ImageCapture");
    public static readonly NavigationRoute VideoCapture = new("VideoCapture");
    public static readonly NavigationRoute ImageEdit = new("ImageEdit");
    public static readonly NavigationRoute VideoEdit = new("VideoEdit");

    public static bool IsMainWindowRoute(NavigationRoute navigationRoute)
    {
        return
            navigationRoute == Home ||
            navigationRoute == Loading ||
            navigationRoute == AddOns ||
            navigationRoute == Error ||
            navigationRoute == About ||
            navigationRoute == Settings ||
            navigationRoute == ImageEdit ||
            navigationRoute == VideoEdit;
    }
}
