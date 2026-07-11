namespace CaptureTool.Application.Abstractions.Telemetry;

public sealed record TelemetryExceptionContext(
    string Component,
    string? ActivityId = null,
    string? UseCaseId = null,
    string? Route = null,
    string? ReasonCode = null,
    bool Fatal = false,
    IReadOnlyDictionary<string, object?>? Attributes = null);
