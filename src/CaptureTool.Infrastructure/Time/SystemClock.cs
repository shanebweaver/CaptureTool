using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;
}
