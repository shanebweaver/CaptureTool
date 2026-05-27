using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.UseCases.Diagnostics;

internal class ClearLogsAppCommand : IClearLogsAppCommand
{
    public ClearLogsAppCommand(ILogService logService)
    {
        _logService = logService;
    }

    private readonly ILogService _logService;

    public void Execute()
    {
        _logService.ClearLogs();
    }
}
