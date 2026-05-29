using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;

public sealed class GetCurrentLogsUseCase : IUseCase<GetCurrentLogsRequest, GetCurrentLogsResponse>
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