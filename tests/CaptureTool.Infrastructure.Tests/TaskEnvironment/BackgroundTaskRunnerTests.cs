using CaptureTool.Application.Abstractions.Logging;
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
        var runner = new BackgroundTaskRunner(logService);

        runner.Run(() => throw exception, failureMessage);

        Assert.IsTrue(logged.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreSame(exception, logService.Exception);
        Assert.AreEqual(failureMessage, logService.Message);
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
