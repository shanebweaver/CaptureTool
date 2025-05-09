using System;
using System.Collections.Generic;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<string, Type> _routeMappings = new() {
        { NavigationRoutes.Home.RouteName, typeof(HomePage) },
        { NavigationRoutes.Settings.RouteName, typeof(SettingsPage) },
        { NavigationRoutes.Loading.RouteName, typeof(LoadingPage) },
        { NavigationRoutes.DesktopImageCaptureOptions.RouteName, typeof(DesktopImageCaptureOptionsPage) },
        { NavigationRoutes.DesktopVideoCaptureOptions.RouteName, typeof(DesktopVideoCaptureOptionsPage) },
        { NavigationRoutes.ImageEdit.RouteName, typeof(ImageEditPage) },
        { NavigationRoutes.VideoEdit.RouteName, typeof(VideoEditPage) },
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
