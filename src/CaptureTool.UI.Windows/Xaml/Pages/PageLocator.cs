using CaptureTool.Core.Implementations.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<object, Type> _routeMappings = new() {
        { CaptureToolNavigationRoute.Home, typeof(HomePage) },
        { CaptureToolNavigationRoute.AddOns, typeof(AddOnsPage) },
        { CaptureToolNavigationRoute.About, typeof(AboutPage) },
        { CaptureToolNavigationRoute.Error, typeof(ErrorPage) },
        { CaptureToolNavigationRoute.Settings, typeof(SettingsPage) },
        { CaptureToolNavigationRoute.Loading, typeof(LoadingPage) },
        { CaptureToolNavigationRoute.ImageEdit, typeof(ImageEditPage) },
        { CaptureToolNavigationRoute.VideoEdit, typeof(VideoEditPage) },
    };

    public static Type GetPageType(object route)
    {
        if (_routeMappings.TryGetValue(route, out var pageType))
        {
            return pageType;
        }

        throw new ArgumentOutOfRangeException(nameof(route));
    }
}
