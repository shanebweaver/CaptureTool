using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Tests;

internal sealed class TestClock : IClock
{
    public static TestClock Instance { get; } = new();

    private TestClock()
    {
    }

    public DateTime Now { get; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);

    public DateTime UtcNow { get; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
}
