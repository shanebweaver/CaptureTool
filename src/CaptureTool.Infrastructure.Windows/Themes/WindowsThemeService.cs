using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.Telemetry;
using Microsoft.Windows.Storage;
using System.Diagnostics;

namespace CaptureTool.Infrastructure.Windows.Themes;

public sealed partial class WindowsThemeService : IThemeService
{
    private readonly ITelemetryService? _telemetryService;

    public AppTheme DefaultTheme { get; private set; }
    public AppTheme StartupTheme { get; private set; }
    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppTheme>? CurrentThemeChanged;

    public WindowsThemeService(ITelemetryService? telemetryService = null)
    {
        _telemetryService = telemetryService;
    }

    public void Initialize(AppTheme defaultTheme)
    {
        Debug.Assert(defaultTheme != AppTheme.SystemDefault);
        DefaultTheme = defaultTheme;

        object? themeValue = ApplicationData.GetDefault().LocalSettings.Values["themeSetting"];
        if (themeValue is int themeValueIndex)
        {
            CurrentTheme = (AppTheme)themeValueIndex;
        }
        else
        {
            CurrentTheme = AppTheme.SystemDefault;
        }

        StartupTheme = CurrentTheme;
    }

    public void UpdateCurrentTheme(AppTheme appTheme)
    {
        if (CurrentTheme != appTheme)
        {
            CurrentTheme = appTheme;
            ApplicationData.GetDefault().LocalSettings.Values["themeSetting"] = (int)appTheme;
            CurrentThemeChanged?.Invoke(this, appTheme);
            _telemetryService?.TrackEvent(
                TelemetryEvents.SettingsChanged,
                new Dictionary<string, object?>
                {
                    [TelemetryProperties.Setting] = "app_theme",
                    [TelemetryProperties.Value] = appTheme.ToString()
                });
        }
    }
}
