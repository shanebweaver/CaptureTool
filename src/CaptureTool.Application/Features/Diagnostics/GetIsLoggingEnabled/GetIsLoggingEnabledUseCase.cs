using CaptureTool.Application.Abstractions.Features.Diagnostics.GetIsLoggingEnabled;
using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Application.Features.Diagnostics.GetIsLoggingEnabled;

public sealed class GetIsLoggingEnabledUseCase : IGetIsLoggingEnabledUseCase
{
    private readonly ILogService _logService;

    public GetIsLoggingEnabledUseCase(ILogService logService)
    {
        _logService = logService;
    }

    public Task<GetIsLoggingEnabledResponse> ExecuteAsync(GetIsLoggingEnabledRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(new GetIsLoggingEnabledResponse(_logService.IsEnabled));
        }
        catch (Exception)
        {
            return Task.FromResult(new GetIsLoggingEnabledResponse(false));
        }
    }
}
