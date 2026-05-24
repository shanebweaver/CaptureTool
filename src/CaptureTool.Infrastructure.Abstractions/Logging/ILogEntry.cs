
namespace CaptureTool.Infrastructure.Abstractions.Logging;

public interface ILogEntry
{
    string Message { get; }
    DateTime Timestamp { get; }
}