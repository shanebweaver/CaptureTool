using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Diagnostics;

internal class DiagnosticsClearLogsAppCommand : IDiagnosticsClearLogsAppCommand
{
    public DiagnosticsClearLogsAppCommand(ILogService logService)
    {
        _logService = logService;
    }

    private readonly ILogService _logService;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _logService.ClearLogs();
    }
}
