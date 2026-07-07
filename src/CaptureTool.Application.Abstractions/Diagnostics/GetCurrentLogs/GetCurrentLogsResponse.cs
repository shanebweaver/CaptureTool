using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;

public sealed record GetCurrentLogsResponse(IEnumerable<ILogEntry> Logs);
