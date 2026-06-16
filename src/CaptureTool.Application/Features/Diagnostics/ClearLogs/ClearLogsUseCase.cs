using CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Diagnostics.ClearLogs;

public sealed class ClearLogsUseCase : IClearLogsUseCase
{
    private const string ActivityId = "ClearLogs";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;

    public ClearLogsUseCase(ILogService logService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
    }

    public Task<UseCaseResponse<ClearLogsResponse>> ExecuteAsync(ClearLogsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _logService.ClearLogs();
                return new ClearLogsResponse();
            },
            cancellationToken: cancellationToken);
    }
}
