using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.ClearLogs;

public sealed class ClearLogsUseCase : IUseCase<ClearLogsRequest, ClearLogsResponse>
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