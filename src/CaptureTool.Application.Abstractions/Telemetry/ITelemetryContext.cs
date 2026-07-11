namespace CaptureTool.Application.Abstractions.Telemetry;

public interface ITelemetryContext
{
    bool IsTelemetryEnabled { get; }
    string SessionId { get; }
    string? InstallIdHash { get; }
    string? CurrentRoute { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    void SetCurrentRoute(object? route);
    IReadOnlyDictionary<string, object?> GetGlobalAttributes();
}
