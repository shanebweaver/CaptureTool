using CaptureTool.Application.Abstractions.Themes;
using Microsoft.Windows.Storage;

namespace CaptureTool.Infrastructure.Windows.Themes;

internal sealed class WindowsThemeSettingsStore : IThemeSettingsStore
{
    private const string ThemeSettingKey = "themeSetting";

    public AppTheme? GetCurrentTheme()
    {
        object? themeValue = ApplicationData.GetDefault().LocalSettings.Values[ThemeSettingKey];
        return themeValue is int themeValueIndex
            ? (AppTheme)themeValueIndex
            : null;
    }

    public void SetCurrentTheme(AppTheme appTheme)
    {
        ApplicationData.GetDefault().LocalSettings.Values[ThemeSettingKey] = (int)appTheme;
    }

    public void ResetCurrentTheme()
    {
        ApplicationData.GetDefault().LocalSettings.Values.Remove(ThemeSettingKey);
    }
}
