using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class TelemetryServiceTests
{
    [TestMethod]
    public void ActivityStatusMethods_ShouldLogActivityAndOptionalMessages()
    {
        var logService = new RecordingLogService();
        var telemetry = new TelemetryService(logService);

        telemetry.ActivityInitiated("capture");
        telemetry.ActivityCanceled("capture", "user canceled");
        telemetry.ActivityCompleted("capture", "saved");

        IReadOnlyList<string> messages = logService.InformationMessages;
        Assert.HasCount(3, messages);
        StringAssert.Contains(messages[0], "Activity initiated: capture");
        StringAssert.Contains(messages[1], "Activity canceled: capture - Message: user canceled");
        StringAssert.Contains(messages[2], "Activity completed: capture - Message: saved");
    }

    [TestMethod]
    public void ActivityError_ShouldLogExceptionWithCallSiteContext()
    {
        var logService = new RecordingLogService();
        var exception = new InvalidOperationException("failure");
        var telemetry = new TelemetryService(logService);

        telemetry.ActivityError(
            "capture",
            exception,
            "while saving",
            caller: "Caller",
            file: @"C:\Source\Capture.cs",
            line: 42,
            exceptionExpr: "exception");

        (Exception loggedException, string? loggedMessage) = logService.Exceptions.Single();
        Assert.AreSame(exception, loggedException);
        Assert.IsNotNull(loggedMessage);
        StringAssert.Contains(loggedMessage, "Activity error: capture");
        StringAssert.Contains(loggedMessage, "Caller: Caller");
        StringAssert.Contains(loggedMessage, "Line: 42");
        StringAssert.Contains(loggedMessage, "File: Capture.cs");
        StringAssert.Contains(loggedMessage, "Exception Expr: exception");
        StringAssert.Contains(loggedMessage, "Message: while saving");
    }

    [TestMethod]
    public void ActivityError_ShouldOmitOptionalContextWhenMissing()
    {
        var logService = new RecordingLogService();
        var telemetry = new TelemetryService(logService);

        telemetry.ActivityError(
            "capture",
            new InvalidOperationException("failure"),
            caller: "Caller",
            file: null,
            line: 42,
            exceptionExpr: null);

        string? loggedMessage = logService.Exceptions.Single().Message;
        Assert.IsNotNull(loggedMessage);
        Assert.IsFalse(loggedMessage.Contains("File:", StringComparison.Ordinal));
        Assert.IsFalse(loggedMessage.Contains("Exception Expr:", StringComparison.Ordinal));
        Assert.IsFalse(loggedMessage.Contains("Message:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ButtonInvoked_ShouldLogButtonMessage()
    {
        var logService = new RecordingLogService();
        var telemetry = new TelemetryService(logService);

        telemetry.ButtonInvoked("save", " from toolbar");

        string message = logService.InformationMessages.Single();
        StringAssert.Contains(message, "Button invoked: save from toolbar");
    }

    private sealed class RecordingLogService : ILogService
    {
        public bool IsEnabled { get; private set; }
        public List<string> InformationMessages { get; } = [];
        public List<(Exception Exception, string? Message)> Exceptions { get; } = [];

        public event EventHandler<ILogEntry>? LogAdded
        {
            add { }
            remove { }
        }

        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public void LogInformation(string info)
        {
            InformationMessages.Add(info);
        }

        public void LogWarning(string warning)
        {
        }

        public void LogException(Exception e, string? message = null)
        {
            Exceptions.Add((e, message));
        }

        public IEnumerable<ILogEntry> GetLogs()
        {
            return [];
        }

        public void ClearLogs()
        {
        }
    }
}
