using System;

namespace CaptureTool.Services.Themes;

public interface IThemeService
{
    AppTheme DefaultTheme { get; }
    AppTheme StartupTheme { get; }
    AppTheme CurrentTheme { get; }

    event EventHandler<AppTheme>? CurrentThemeChanged;

    void Initialize(AppTheme defualtTheme);
    void UpdateCurrentTheme(AppTheme appTheme);
}