using CaptureTool.Application.Abstractions.Themes;

namespace CaptureTool.Infrastructure.Windows.Themes;

internal interface IThemeSettingsStore
{
    AppTheme? GetCurrentTheme();

    void SetCurrentTheme(AppTheme appTheme);

    void ResetCurrentTheme();
}
