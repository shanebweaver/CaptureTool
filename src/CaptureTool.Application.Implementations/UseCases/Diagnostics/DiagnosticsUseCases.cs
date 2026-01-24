using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.UseCases.Diagnostics;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Diagnostics;

public sealed class DiagnosticsUseCases : IDiagnosticsUseCases
{
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public DiagnosticsUseCases(
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
