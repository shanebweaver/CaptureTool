using CaptureTool.Core.Interfaces.Actions.Diagnostics;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Diagnostics;

public sealed class DiagnosticsActions : IDiagnosticsActions
{
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public DiagnosticsActions(
        ILogService logService,
        ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    public async Task UpdateLoggingStateAsync(bool enabled, CancellationToken ct)
    {
        if (enabled)
        {
            _logService.Enable();
        }
        else
        {
            _logService.Disable();
        }

        _settingsService.Set(CaptureToolSettings.VerboseLogging, enabled);
        await _settingsService.TrySaveAsync(ct);
    }

    public void ClearLogs()
    {
        _logService.ClearLogs();
    }

    public IEnumerable<ILogEntry> GetCurrentLogs()
    {
        return _logService.GetLogs();
    }

    public bool IsLoggingEnabled()
    {
        return _logService.IsEnabled;
    }
}
