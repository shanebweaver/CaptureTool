using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;

public sealed record GetCurrentLogsResponse(IEnumerable<ILogEntry> Logs);
