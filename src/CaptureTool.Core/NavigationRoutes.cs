using CaptureTool.Services.Navigation;

namespace CaptureTool.Core;

public static partial class NavigationRoutes
{
    public static readonly NavigationRoute Loading = new("Loading");
    public static readonly NavigationRoute Home = new("Home");
    public static readonly NavigationRoute Settings = new("Settings");
    public static readonly NavigationRoute About = new("About");
    public static readonly NavigationRoute ImageEdit = new("ImageEdit");
    public static readonly NavigationRoute DesktopImageCaptureOptions = new("DesktopImageCaptureOptions");
    public static readonly NavigationRoute DesktopVideoCaptureOptions = new("DesktopVideoCaptureOptions");
    public static readonly NavigationRoute DesktopAudioCaptureOptions = new("DesktopAudioCaptureOptions");
}
