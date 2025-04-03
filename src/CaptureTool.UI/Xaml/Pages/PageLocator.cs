using System;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

internal class PageLocator
{
    public static Type GetPageType(NavigationRoute route)
    {
        if (route == NavigationRoutes.Home)
        {
            return typeof(HomePage);
        }
        else if (route == NavigationRoutes.Settings)
        {
            return typeof(SettingsPage);
        }
        else if (route == NavigationRoutes.About)
        {
            return typeof(AboutPage);
        }
        else if (route == NavigationRoutes.ImageEdit)
        {
            return typeof(ImageEditPage);
        }
        else if (route == NavigationRoutes.Loading)
        {
            return typeof(LoadingPage);
        }
        else
        {
            throw new ArgumentOutOfRangeException(route.RouteName);
        };
    }
}
