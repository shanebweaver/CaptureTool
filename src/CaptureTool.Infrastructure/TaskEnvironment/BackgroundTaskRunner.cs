using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.TaskEnvironment;

public sealed class BackgroundTaskRunner : IBackgroundTaskRunner
{
    private readonly ILogService _logService;
    private readonly ITelemetryService _telemetryService;

    public BackgroundTaskRunner(
        ILogService logService,
        ITelemetryService telemetryService)
    {
        _logService = logService;
        _telemetryService = telemetryService;
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
                _telemetryService.TrackException(
                    ex,
                    new TelemetryExceptionContext(
                        Component: "BackgroundTask",
                        ActivityId: "background_task",
                        ReasonCode: "background_task_failed"));
                _logService.LogException(ex, failureMessage);
            }
        });
    }
}
