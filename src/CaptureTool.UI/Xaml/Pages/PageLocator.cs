using System;
using System.Collections.Generic;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<string, Type> _routeMappings = new() {
        { CaptureToolNavigationRoutes.Home.RouteName, typeof(HomePage) },
        { CaptureToolNavigationRoutes.Settings.RouteName, typeof(SettingsPage) },
        { CaptureToolNavigationRoutes.Loading.RouteName, typeof(LoadingPage) },
        { CaptureToolNavigationRoutes.DesktopImageCaptureOptions.RouteName, typeof(DesktopImageCaptureOptionsPage) },
        { CaptureToolNavigationRoutes.DesktopVideoCaptureOptions.RouteName, typeof(DesktopVideoCaptureOptionsPage) },
        { CaptureToolNavigationRoutes.ImageEdit.RouteName, typeof(ImageEditPage) },
        { CaptureToolNavigationRoutes.VideoEdit.RouteName, typeof(VideoEditPage) },
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
