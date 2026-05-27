using CaptureTool.Application.UseCases.Navigation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<object, Type> _routeMappings = new() {
        { NavigationRoute.Home, typeof(HomePage) },
        { NavigationRoute.Store, typeof(AddOnsPage) },
        { NavigationRoute.About, typeof(AboutPage) },
        { NavigationRoute.Error, typeof(ErrorPage) },
        { NavigationRoute.Settings, typeof(SettingsPage) },
        { NavigationRoute.Loading, typeof(LoadingPage) },
        { NavigationRoute.ImageEdit, typeof(ImageEditPage) },
        { NavigationRoute.VideoEdit, typeof(VideoEditPage) },
        { NavigationRoute.AudioCapture, typeof(AudioCapturePage) },
        { NavigationRoute.AudioEdit, typeof(AudioEditPage) },
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
