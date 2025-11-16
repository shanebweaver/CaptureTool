using System;
using System.Collections.Generic;
using CaptureTool.Core.Navigation;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Windows.Xaml.Pages;

internal static partial class PageLocator
{
    private static readonly Dictionary<object, Type> _routeMappings = new() {
        { NavigationRoute.Home, typeof(HomePage) },
        { NavigationRoute.AddOns, typeof(AddOnsPage) },
        { NavigationRoute.About, typeof(AboutPage) },
        { NavigationRoute.Error, typeof(ErrorPage) },
        { NavigationRoute.Settings, typeof(SettingsPage) },
        { NavigationRoute.Loading, typeof(LoadingPage) },
        { NavigationRoute.ImageEdit, typeof(ImageEditPage) },
        { NavigationRoute.VideoEdit, typeof(VideoEditPage) },
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
