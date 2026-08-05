using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.Telemetry;
using System.Diagnostics;

namespace CaptureTool.Infrastructure.Windows.Themes;

public sealed partial class WindowsThemeService : IThemeService
{
    private readonly ITelemetryService? _telemetryService;
    private readonly IThemeSettingsStore _settingsStore;

    public AppTheme DefaultTheme { get; private set; }
    public AppTheme StartupTheme { get; private set; }
    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppTheme>? CurrentThemeChanged;

    public WindowsThemeService(ITelemetryService? telemetryService = null)
        : this(new WindowsThemeSettingsStore(), telemetryService)
    {
    }

    internal WindowsThemeService(
        IThemeSettingsStore settingsStore,
        ITelemetryService? telemetryService = null)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);

        _settingsStore = settingsStore;
        _telemetryService = telemetryService;
    }

    public void Initialize(AppTheme defaultTheme)
    {
        Debug.Assert(defaultTheme != AppTheme.SystemDefault);
        DefaultTheme = defaultTheme;

        CurrentTheme = _settingsStore.GetCurrentTheme() ?? AppTheme.SystemDefault;

        StartupTheme = CurrentTheme;
    }

    public void UpdateCurrentTheme(AppTheme appTheme)
    {
        if (CurrentTheme != appTheme)
        {
            CurrentTheme = appTheme;
            _settingsStore.SetCurrentTheme(appTheme);
            CurrentThemeChanged?.Invoke(this, appTheme);
            TrackThemeChanged(appTheme);
        }
    }

    public void ResetCurrentTheme()
    {
        _settingsStore.ResetCurrentTheme();
        if (CurrentTheme == AppTheme.SystemDefault)
        {
            return;
        }

        CurrentTheme = AppTheme.SystemDefault;
        CurrentThemeChanged?.Invoke(this, CurrentTheme);
        TrackThemeChanged(CurrentTheme);
    }

    private void TrackThemeChanged(AppTheme appTheme)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Setting] = "app_theme",
                [TelemetryProperties.Value] = appTheme.ToString()
            });
    }
}
