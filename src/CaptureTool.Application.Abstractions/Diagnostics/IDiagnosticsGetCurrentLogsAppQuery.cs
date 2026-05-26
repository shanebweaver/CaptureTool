using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Queries;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IDiagnosticsGetCurrentLogsAppQuery : IAppQuery<IEnumerable<ILogEntry>>
{
}
