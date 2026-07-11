namespace CaptureTool.Mcp.CaptureServer.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
