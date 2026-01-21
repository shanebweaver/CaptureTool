
namespace CaptureTool.Infrastructure.Interfaces.Logging;

public interface ILogEntry
{
    string Message { get; }
    DateTime Timestamp { get; }
}