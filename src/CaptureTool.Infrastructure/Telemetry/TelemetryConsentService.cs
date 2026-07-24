using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Telemetry;

public sealed class TelemetryConsentService : ITelemetryConsentService
{
    private int _state = (int)TelemetryConsentState.Unknown;

    public TelemetryConsentState State => (TelemetryConsentState)Volatile.Read(ref _state);

    public void SetState(TelemetryConsentState state)
    {
        Volatile.Write(ref _state, (int)state);
    }
}
