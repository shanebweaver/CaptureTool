using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Diagnostics;

internal class DiagnosticsGetCurrentLogsAppQuery : IDiagnosticsGetCurrentLogsAppQuery
{
    public DiagnosticsGetCurrentLogsAppQuery(ILogService logService)
    {
        _logService = logService;
    }
    
    private readonly ILogService _logService;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<ILogEntry> Execute()
    {
        return _logService.GetLogs();
    }
}
