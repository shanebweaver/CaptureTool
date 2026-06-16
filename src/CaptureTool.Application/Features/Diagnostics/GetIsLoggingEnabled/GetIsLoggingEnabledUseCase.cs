using CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;

public sealed class GetIsLoggingEnabledUseCase : IGetIsLoggingEnabledUseCase
{
    private const string ActivityId = "GetIsLoggingEnabled";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;

    public GetIsLoggingEnabledUseCase(ILogService logService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
    }

    public Task<UseCaseResponse<GetIsLoggingEnabledResponse>> ExecuteAsync(GetIsLoggingEnabledRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                return new GetIsLoggingEnabledResponse(_logService.IsEnabled);
            },
            cancellationToken: cancellationToken);
    }
}
