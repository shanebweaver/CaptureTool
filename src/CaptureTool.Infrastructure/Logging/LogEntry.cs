using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Infrastructure.Logging;

public readonly struct LogEntry : ILogEntry
{
    public DateTime Timestamp { get; }
    public string Message { get; }

    public LogEntry(string message)
    {
        Timestamp = DateTime.Now;
        Message = message;
    }

    public override string ToString() => $"{Timestamp:HH:mm:ss} - {Message}";
}
