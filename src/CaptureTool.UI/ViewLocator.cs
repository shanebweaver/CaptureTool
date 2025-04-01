using System;
using CaptureTool.Core;
using CaptureTool.UI.Xaml.Pages;

namespace CaptureTool.UI;

internal class ViewLocator
{
    public static Type GetViewType(string key)
    {
        return key switch
        {
            NavigationKeys.Home => typeof(HomePage),
            NavigationKeys.Settings => typeof(SettingsPage),
            NavigationKeys.About => typeof(AboutPage),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
    }
}
