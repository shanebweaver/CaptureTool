using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Diagnostics;

internal class DiagnosticsIsLoggingEnabledAppQuery : IDiagnosticsIsLoggingEnabledAppQuery
{
    private readonly ILogService _logService;

    public DiagnosticsIsLoggingEnabledAppQuery(ILogService logService)
    {
        _logService = logService;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public bool Execute()
    {
        return _logService.IsEnabled;
    }
}
