using System;
using System.Collections.Generic;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<string, Type> _routeMappings = new() {
        { CaptureToolNavigationRoutes.Home.RouteName, typeof(HomePage) },
        { CaptureToolNavigationRoutes.About.RouteName, typeof(AboutPage) },
        { CaptureToolNavigationRoutes.Error.RouteName, typeof(ErrorPage) },
        { CaptureToolNavigationRoutes.Settings.RouteName, typeof(SettingsPage) },
        { CaptureToolNavigationRoutes.Loading.RouteName, typeof(LoadingPage) },
        { CaptureToolNavigationRoutes.ImageEdit.RouteName, typeof(ImageEditPage) },
    };

    public static Type GetPageType(NavigationRoute route)
    {
        if (_routeMappings.TryGetValue(route.RouteName, out var pageType))
        {
            return pageType;
        }

        throw new ArgumentOutOfRangeException(route.RouteName);
    }
}
