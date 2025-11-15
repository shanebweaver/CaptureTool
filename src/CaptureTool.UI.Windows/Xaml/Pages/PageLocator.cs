using System;
using System.Collections.Generic;
using CaptureTool.Core.Navigation;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<string, Type> _routeMappings = new() {
        { CaptureToolNavigationRoutes.Home.Id, typeof(HomePage) },
        { CaptureToolNavigationRoutes.AddOns.Id, typeof(AddOnsPage) },
        { CaptureToolNavigationRoutes.About.Id, typeof(AboutPage) },
        { CaptureToolNavigationRoutes.Error.Id, typeof(ErrorPage) },
        { CaptureToolNavigationRoutes.Settings.Id, typeof(SettingsPage) },
        { CaptureToolNavigationRoutes.Loading.Id, typeof(LoadingPage) },
        { CaptureToolNavigationRoutes.ImageEdit.Id, typeof(ImageEditPage) },
        { CaptureToolNavigationRoutes.VideoEdit.Id, typeof(VideoEditPage) },
    };

    public static Type GetPageType(NavigationRoute route)
    {
        if (_routeMappings.TryGetValue(route.Id, out var pageType))
        {
            return pageType;
        }

        throw new ArgumentOutOfRangeException(route.Id);
    }
}
