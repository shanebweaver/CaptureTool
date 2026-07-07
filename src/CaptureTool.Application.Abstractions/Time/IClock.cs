namespace CaptureTool.Application.Abstractions.Time;

public interface IClock
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
}
