using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Telemetry;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Telemetry;

public sealed class TelemetryContext : ITelemetryContext, IDisposable
{
    private const string AppName = "CaptureTool";
    private const string SchemaVersion = "1";

    private readonly ISettingsService _settingsService;
    private bool _disposed;

    public TelemetryContext(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
        SessionId = Guid.NewGuid().ToString("N");
    }

    public bool IsTelemetryEnabled { get; private set; }
    public string SessionId { get; }
    public string? InstallIdHash { get; private set; }
    public string? CurrentRoute { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsTelemetryEnabled = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_IsEnabled);

        string installId = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_InstallId);
        if (string.IsNullOrWhiteSpace(installId))
        {
            installId = Guid.NewGuid().ToString("N");
            _settingsService.Set(CaptureToolSettings.Settings_Telemetry_InstallId, installId);
            await _settingsService.TrySaveAsync(cancellationToken);
        }

        InstallIdHash = HashInstallId(installId);
    }

    public void SetCurrentRoute(object? route)
    {
        CurrentRoute = route?.ToString();
    }

    public IReadOnlyDictionary<string, object?> GetGlobalAttributes()
    {
        Dictionary<string, object?> attributes = new()
        {
            [TelemetryAttributes.AppName] = AppName,
            [TelemetryAttributes.AppBuildChannel] = GetBuildChannel(),
            [TelemetryAttributes.AppVersion] = GetAppVersion(),
            [TelemetryAttributes.SchemaVersion] = SchemaVersion,
            [TelemetryAttributes.SessionId] = SessionId
        };

        if (!string.IsNullOrWhiteSpace(InstallIdHash))
        {
            attributes[TelemetryAttributes.InstallIdHash] = InstallIdHash;
        }

        if (!string.IsNullOrWhiteSpace(CurrentRoute))
        {
            attributes[TelemetryAttributes.CurrentRoute] = CurrentRoute;
        }

        return attributes;
    }

    private void OnSettingsChanged(ISettingDefinition[] settingDefinitions)
    {
        if (settingDefinitions.Any(setting => setting.Key == CaptureToolSettings.Settings_Telemetry_IsEnabled.Key))
        {
            IsTelemetryEnabled = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_IsEnabled);
        }

        if (settingDefinitions.Any(setting => setting.Key == CaptureToolSettings.Settings_Telemetry_InstallId.Key))
        {
            string installId = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_InstallId);
            InstallIdHash = string.IsNullOrWhiteSpace(installId)
                ? null
                : HashInstallId(installId);
        }
    }

    private static string HashInstallId(string installId)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(installId));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetAppVersion()
    {
        return typeof(TelemetryContext).Assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string GetBuildChannel()
    {
#if DEBUG
        return "debug";
#else
        return "release";
#endif
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _settingsService.SettingsChanged -= OnSettingsChanged;
        _disposed = true;
    }
}
