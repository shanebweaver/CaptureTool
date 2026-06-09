using CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.ClearLogs;

public sealed class ClearLogsUseCase : IClearLogsUseCase
{
    private readonly ILogService _logService;

    public ClearLogsUseCase(ILogService logService)
    {
        _logService = logService;
    }

    public Task<ClearLogsResponse> ExecuteAsync(ClearLogsRequest request, CancellationToken cancellationToken = default)
    {
        _logService.ClearLogs();
        return Task.FromResult(new ClearLogsResponse());
    }
}