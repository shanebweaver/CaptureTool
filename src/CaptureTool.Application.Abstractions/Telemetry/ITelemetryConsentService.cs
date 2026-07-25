namespace CaptureTool.Application.Abstractions.Telemetry;

public interface ITelemetryConsentService
{
    TelemetryConsentState State { get; }

    void SetState(TelemetryConsentState state);
}
