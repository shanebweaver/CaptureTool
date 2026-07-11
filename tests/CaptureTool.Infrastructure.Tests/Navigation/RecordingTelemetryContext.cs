using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Navigation;

internal sealed class RecordingTelemetryContext : ITelemetryContext
{
    public bool IsTelemetryEnabled => true;
    public string SessionId { get; } = "session";
    public string? InstallIdHash { get; } = "install";
    public string? CurrentRoute { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void SetCurrentRoute(object? route)
    {
        CurrentRoute = route?.ToString();
    }

    public IReadOnlyDictionary<string, object?> GetGlobalAttributes()
    {
        return new Dictionary<string, object?>();
    }
}
