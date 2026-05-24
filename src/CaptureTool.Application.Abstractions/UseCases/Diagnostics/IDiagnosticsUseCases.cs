using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Abstractions.UseCases.Diagnostics;

public interface IDiagnosticsUseCases
{
    Task UpdateLoggingStateAsync(bool enabled, CancellationToken ct);
    void ClearLogs();
    IEnumerable<ILogEntry> GetCurrentLogs();
    bool IsLoggingEnabled();
}
