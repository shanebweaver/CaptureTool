using CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;

public sealed class GetCurrentLogsUseCase : IGetCurrentLogsUseCase
{
    private readonly ILogService _logService;

    public GetCurrentLogsUseCase(ILogService logService)
    {
        _logService = logService;
    }

    public Task<GetCurrentLogsResponse> ExecuteAsync(GetCurrentLogsRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GetCurrentLogsResponse(_logService.GetLogs()));
    }
}