using System;
using Microsoft.Windows.Storage;

namespace CaptureTool.Services.Themes.Windows;

public sealed partial class WindowsThemeService : IThemeService
{
    public AppTheme DefaultTheme { get; private set; }
    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppTheme>? CurrentThemeChanged;

    public WindowsThemeService()
    {
        object? themeValue = ApplicationData.GetDefault().LocalSettings.Values["themeSetting"];
        if (themeValue is int themeValueIndex)
        {
            CurrentTheme = (AppTheme)themeValueIndex;
        }
    }

    public void UpdateCurrentTheme(AppTheme appTheme)
    {
        if (CurrentTheme != appTheme)
        {
            CurrentTheme = appTheme;
            ApplicationData.GetDefault().LocalSettings.Values["themeSetting"] = (int)appTheme;
            CurrentThemeChanged?.Invoke(this, appTheme);
        }
    }

    public void SetDefaultTheme(AppTheme defaultTheme)
    {
        DefaultTheme = defaultTheme;
    }
}
