using CaptureTool.Services.Navigation;

namespace CaptureTool.Core;

public static partial class CaptureToolNavigationRoutes
{
    public static readonly NavigationRoute Loading = new("Loading");
    public static readonly NavigationRoute Error = new("Error");
    public static readonly NavigationRoute Home = new("Home");
    public static readonly NavigationRoute Settings = new("Settings");
    public static readonly NavigationRoute About = new("About");
    public static readonly NavigationRoute ImageEdit = new("ImageEdit");
    public static readonly NavigationRoute VideoEdit = new("VideoEdit");
    public static readonly NavigationRoute AudioEdit = new("AudioEdit");
    public static readonly NavigationRoute ImageCaptureOptions = new("ImageCaptureOptions");
    public static readonly NavigationRoute VideoCaptureOptions = new("VideoCaptureOptions");
    public static readonly NavigationRoute AudioCaptureOptions = new("AudioCaptureOptions");
}
