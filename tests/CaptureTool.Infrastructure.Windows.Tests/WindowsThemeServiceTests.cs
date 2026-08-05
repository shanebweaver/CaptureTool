using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Windows.Themes;

namespace CaptureTool.Infrastructure.Windows.Tests;

[TestClass]
public sealed class WindowsThemeServiceTests
{
    [TestMethod]
    public void ResetCurrentTheme_ShouldApplyImmediatelyAndRemoveNextLaunchOverride()
    {
        var settingsStore = new TestThemeSettingsStore { CurrentTheme = AppTheme.Dark };
        var telemetry = new RecordingTelemetryService();
        var service = new WindowsThemeService(settingsStore, telemetry);
        service.Initialize(AppTheme.Light);
        AppTheme? changedTheme = null;
        service.CurrentThemeChanged += (_, appTheme) => changedTheme = appTheme;

        service.ResetCurrentTheme();

        Assert.AreEqual(AppTheme.SystemDefault, service.CurrentTheme);
        Assert.AreEqual(AppTheme.SystemDefault, changedTheme);
        Assert.IsNull(settingsStore.CurrentTheme);
        Assert.AreEqual(1, settingsStore.ResetCount);
        Assert.HasCount(1, telemetry.Events);
        Assert.AreEqual(
            AppTheme.SystemDefault.ToString(),
            telemetry.Events[0].Properties[TelemetryProperties.Value]);

        var nextLaunchService = new WindowsThemeService(settingsStore);
        nextLaunchService.Initialize(AppTheme.Dark);

        Assert.AreEqual(AppTheme.SystemDefault, nextLaunchService.CurrentTheme);
    }

    [TestMethod]
    public void UpdateCurrentTheme_ShouldPersistSelectedTheme()
    {
        var settingsStore = new TestThemeSettingsStore();
        var service = new WindowsThemeService(settingsStore);
        service.Initialize(AppTheme.Light);

        service.UpdateCurrentTheme(AppTheme.Dark);

        Assert.AreEqual(AppTheme.Dark, service.CurrentTheme);
        Assert.AreEqual(AppTheme.Dark, settingsStore.CurrentTheme);
    }

    private sealed class RecordingTelemetryService : ITelemetryService
    {
        public List<(string Name, IReadOnlyDictionary<string, object?> Properties)> Events { get; } = [];

        public void TrackEvent(
            string eventName,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add((eventName, properties ?? new Dictionary<string, object?>()));
        }
    }

    private sealed class TestThemeSettingsStore : IThemeSettingsStore
    {
        public AppTheme? CurrentTheme { get; set; }

        public int ResetCount { get; private set; }

        public AppTheme? GetCurrentTheme() => CurrentTheme;

        public void SetCurrentTheme(AppTheme appTheme) => CurrentTheme = appTheme;

        public void ResetCurrentTheme()
        {
            CurrentTheme = null;
            ResetCount++;
        }
    }
}
