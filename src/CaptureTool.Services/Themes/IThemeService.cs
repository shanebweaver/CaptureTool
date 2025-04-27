using System;

namespace CaptureTool.Services.Themes;

public interface IThemeService
{
    AppTheme DefaultTheme { get; }
    AppTheme CurrentTheme { get; }

    event EventHandler<AppTheme>? CurrentThemeChanged;

    void SetDefaultTheme(AppTheme defualtTheme);
    void UpdateCurrentTheme(AppTheme appTheme);
}