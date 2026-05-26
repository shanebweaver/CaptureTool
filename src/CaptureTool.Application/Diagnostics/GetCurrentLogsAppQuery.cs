using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Diagnostics;

internal class GetCurrentLogsAppQuery : IGetCurrentLogsAppQuery
{
    public GetCurrentLogsAppQuery(ILogService logService)
    {
        _logService = logService;
    }
    
    private readonly ILogService _logService;

    public IEnumerable<ILogEntry> Execute()
    {
        return _logService.GetLogs();
    }
}
