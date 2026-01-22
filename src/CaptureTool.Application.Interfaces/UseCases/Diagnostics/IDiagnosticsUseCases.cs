using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Application.Interfaces.UseCases.Diagnostics;

public interface IDiagnosticsUseCases
{
    Task UpdateLoggingStateAsync(bool enabled, CancellationToken ct);
    void ClearLogs();
    IEnumerable<ILogEntry> GetCurrentLogs();
    bool IsLoggingEnabled();
}
