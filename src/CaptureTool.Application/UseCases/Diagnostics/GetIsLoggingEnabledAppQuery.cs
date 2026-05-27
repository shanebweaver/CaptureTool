using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Application.UseCases.Diagnostics;

internal class GetIsLoggingEnabledAppQuery : IGetIsLoggingEnabledAppQuery
{
    private readonly ILogService _logService;

    public GetIsLoggingEnabledAppQuery(ILogService logService)
    {
        _logService = logService;
    }

    public bool Execute()
    {
        return _logService.IsEnabled;
    }
}
