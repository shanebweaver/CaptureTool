using System;
using CaptureTool.Common;
using CaptureTool.UI.Xaml.Pages;

namespace CaptureTool.UI;

internal class ViewLocator
{
    public static Type GetViewType(string key)
    {
        return key switch
        {
            NavigationKeys.Home => typeof(HomePage),
            //NavigationKeys.Settings => typeof(SettingsPage),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
    }
}
