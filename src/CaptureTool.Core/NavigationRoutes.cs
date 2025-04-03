using CaptureTool.Services.Navigation;

namespace CaptureTool.Core;

public static partial class NavigationRoutes
{
    public static readonly NavigationRoute Loading = new("Loading");
    public static readonly NavigationRoute Home = new("Home");
    public static readonly NavigationRoute Settings = new("Settings");
    public static readonly NavigationRoute About = new("About");
    public static readonly NavigationRoute ImageEdit = new("ImageEdit");
}
