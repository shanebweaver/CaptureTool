using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Infrastructure.Logging;

namespace CaptureTool.Infrastructure.Tests.Logging;

[TestClass]
public sealed class LogServiceTests
{
    [TestMethod]
    public void LogInformation_WhenDisabled_DoesNotStoreEntriesOrRaiseEvent()
    {
        var service = new ShortTermMemoryLogService();
        int eventCount = 0;
        service.LogAdded += (_, _) => eventCount++;

        service.LogInformation("message");

        Assert.IsEmpty(service.GetLogs());
        Assert.AreEqual(0, eventCount);
    }

    [TestMethod]
    public void LogWarning_WhenEnabled_StoresNewestFirstAndRaisesEvent()
    {
        var service = new ShortTermMemoryLogService();
        var raisedEntries = new List<ILogEntry>();
        service.LogAdded += (_, entry) => raisedEntries.Add(entry);

        service.Enable();
        service.LogInformation("first");
        service.LogWarning("second");

        var logs = service.GetLogs().ToArray();
        Assert.HasCount(2, logs);
        StringAssert.StartsWith(logs[0].Message, "WARNING: second");
        StringAssert.StartsWith(logs[1].Message, "INFO: first");
        Assert.HasCount(2, raisedEntries);
    }

    [TestMethod]
    public void LogException_IncludesExceptionMessageCustomMessageAndStackTrace()
    {
        var service = new ShortTermMemoryLogService();
        service.Enable();

        try
        {
            ThrowTestException();
        }
        catch (InvalidOperationException exception)
        {
            service.LogException(exception, "while testing");
        }

        string message = service.GetLogs().Single().Message;
        StringAssert.Contains(message, "ERROR: test failure");
        StringAssert.Contains(message, "while testing");
        StringAssert.Contains(message, nameof(ThrowTestException));
    }

    [TestMethod]
    public void ClearLogs_RemovesStoredEntries()
    {
        var service = new ShortTermMemoryLogService();
        service.Enable();
        service.LogInformation("message");

        service.ClearLogs();

        Assert.IsEmpty(service.GetLogs());
    }

    [TestMethod]
    public void LogEntry_ToString_UsesTimestampAndMessage()
    {
        var entry = new LogEntry("hello");

        string result = entry.ToString();

        StringAssert.Contains(result, " - hello");
    }

    private static void ThrowTestException()
    {
        throw new InvalidOperationException("test failure");
    }
}
