using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Queries;

namespace CaptureTool.Application.Abstractions.Diagnostics;

public interface IGetCurrentLogsAppQuery : IAppQuery<IEnumerable<ILogEntry>>
{
}
