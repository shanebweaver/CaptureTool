using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.TaskEnvironment;

namespace CaptureTool.Infrastructure.TaskEnvironment;

public sealed class BackgroundTaskRunner : IBackgroundTaskRunner
{
    private readonly ILogService _logService;

    public BackgroundTaskRunner(ILogService logService)
    {
        _logService = logService;
    }

    public void Run(Action action, string failureMessage)
    {
        _ = Task.Run(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, failureMessage);
            }
        });
    }
}
