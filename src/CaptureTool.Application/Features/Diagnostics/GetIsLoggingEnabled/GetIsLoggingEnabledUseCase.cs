using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;

public sealed class GetIsLoggingEnabledUseCase : IUseCase<GetIsLoggingEnabledRequest, GetIsLoggingEnabledResponse>
{
    private readonly ILogService _logService;

    public GetIsLoggingEnabledUseCase(ILogService logService)
    {
        _logService = logService;
    }

    public Task<GetIsLoggingEnabledResponse> ExecuteAsync(GetIsLoggingEnabledRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GetIsLoggingEnabledResponse(_logService.IsEnabled));
    }
}