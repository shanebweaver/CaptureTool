using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.TaskEnvironment;

namespace CaptureTool.Infrastructure.Tests.TaskEnvironment;

[TestClass]
public sealed class BackgroundTaskRunnerTests
{
    [TestMethod]
    public void Run_WhenActionThrows_LogsException()
    {
        using ManualResetEventSlim logged = new();
        var exception = new InvalidOperationException("Failed.");
        const string failureMessage = "Background task failed.";
        var logService = new TestLogService(logged);
        var telemetry = new TestTelemetryService();
        var runner = new BackgroundTaskRunner(logService, telemetry);

        runner.Run(() => throw exception, failureMessage);

        Assert.IsTrue(logged.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreSame(exception, logService.Exception);
        Assert.AreEqual(failureMessage, logService.Message);
        Assert.AreSame(exception, telemetry.Exception);
        Assert.AreEqual("BackgroundTask", telemetry.ExceptionContext?.Component);
        Assert.AreEqual("background_task_failed", telemetry.ExceptionContext?.ReasonCode);
    }

    private sealed class TestTelemetryService : ITelemetryService
    {
        public Exception? Exception { get; private set; }
        public TelemetryExceptionContext? ExceptionContext { get; private set; }

        public IDisposable? StartActivity(string name, IReadOnlyDictionary<string, object?>? attributes = null) => null;

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void TrackException(Exception exception, TelemetryExceptionContext context)
        {
            Exception = exception;
            ExceptionContext = context;
        }

        public void TrackMetric(string metricName, double value, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void ActivityInitiated(string activityId, string? message = null)
        {
        }

        public void ActivityCompleted(string activityId, string? message = null)
        {
        }

        public void ActivityCanceled(string activityId, string? message = null)
        {
        }

        public void ActivityError(
            string activityId,
            Exception e,
            string? message = null,
            string? callerMemberName = null,
            string? callerFilePath = null,
            int callerLineNumber = 0,
            string? stackTrace = null)
        {
        }

        public void ButtonInvoked(string buttonId, string? message)
        {
        }
    }

    private sealed class TestLogService(ManualResetEventSlim logged) : ILogService
    {
        public Exception? Exception { get; private set; }
        public string? Message { get; private set; }
        public bool IsEnabled => true;
        public event EventHandler<ILogEntry>? LogAdded
        {
            add { }
            remove { }
        }

        public void Enable()
        {
        }

        public void Disable()
        {
        }

        public void LogInformation(string info)
        {
        }

        public void LogWarning(string warning)
        {
        }

        public void LogException(Exception e, string? message = null)
        {
            Exception = e;
            Message = message;
            logged.Set();
        }

        public IEnumerable<ILogEntry> GetLogs() => [];

        public void ClearLogs()
        {
        }
    }
}
