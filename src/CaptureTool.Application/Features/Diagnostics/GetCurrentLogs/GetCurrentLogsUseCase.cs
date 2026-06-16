using CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Diagnostics.GetCurrentLogs;

public sealed class GetCurrentLogsUseCase : IGetCurrentLogsUseCase
{
    private const string ActivityId = "GetCurrentLogs";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;

    public GetCurrentLogsUseCase(ILogService logService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
    }

    public Task<UseCaseResponse<GetCurrentLogsResponse>> ExecuteAsync(GetCurrentLogsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                return new GetCurrentLogsResponse(_logService.GetLogs());
            },
            cancellationToken: cancellationToken);
    }
}
