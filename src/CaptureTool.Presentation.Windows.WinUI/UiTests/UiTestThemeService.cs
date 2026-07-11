using CaptureTool.Application.Abstractions.Themes;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestThemeService : IThemeService
{
    public AppTheme DefaultTheme { get; private set; }
    public AppTheme StartupTheme { get; private set; }
    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppTheme>? CurrentThemeChanged;

    public void Initialize(AppTheme defualtTheme)
    {
        DefaultTheme = defualtTheme;
        CurrentTheme = AppTheme.SystemDefault;
        StartupTheme = CurrentTheme;
    }

    public void UpdateCurrentTheme(AppTheme appTheme)
    {
        if (CurrentTheme == appTheme)
        {
            return;
        }

        CurrentTheme = appTheme;
        CurrentThemeChanged?.Invoke(this, appTheme);
    }
}
