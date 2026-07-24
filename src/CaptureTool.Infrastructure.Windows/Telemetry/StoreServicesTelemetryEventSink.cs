using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Telemetry;
using Microsoft.Services.Store.Engagement;

namespace CaptureTool.Infrastructure.Windows.Telemetry;

public sealed class StoreServicesTelemetryEventSink : ITelemetryEventSink
{
    private readonly ILogService _logService;
    private readonly Lazy<StoreServicesCustomEventLogger> _logger =
        new(StoreServicesCustomEventLogger.GetDefault);
    private int _isDisabled;

    public StoreServicesTelemetryEventSink(ILogService logService)
    {
        _logService = logService;
    }

    public void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (Volatile.Read(ref _isDisabled) != 0)
        {
            return;
        }

        try
        {
            string partnerCenterEventName =
                PartnerCenterTelemetryEventNameFormatter.Format(eventName, properties);
            _logger.Value.Log(partnerCenterEventName);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref _isDisabled, 1);
            _logService.LogException(
                exception,
                "Microsoft Store Services telemetry is unavailable for this app session.");
        }
    }
}
