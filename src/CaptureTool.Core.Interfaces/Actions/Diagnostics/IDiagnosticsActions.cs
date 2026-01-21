using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Core.Interfaces.Actions.Diagnostics;

public interface IDiagnosticsActions
{
    Task UpdateLoggingStateAsync(bool enabled, CancellationToken ct);
    void ClearLogs();
    IEnumerable<ILogEntry> GetCurrentLogs();
    bool IsLoggingEnabled();
}
