using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Telemetry;

/// <summary>
/// Prevents telemetry from reaching its destination until the user opts in.
/// </summary>
public sealed class ConsentAwareTelemetryService : ITelemetryService
{
    private readonly ITelemetryConsentService _consentService;
    private readonly ITelemetryEventSink _eventSink;

    public ConsentAwareTelemetryService(
        ITelemetryConsentService consentService,
        ITelemetryEventSink eventSink)
    {
        _consentService = consentService;
        _eventSink = eventSink;
    }

    public void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (_consentService.State != TelemetryConsentState.Granted)
        {
            return;
        }

        _eventSink.TrackEvent(eventName, properties);
    }
}
