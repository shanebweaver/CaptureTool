using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class ConsentAwareTelemetryServiceTests
{
    [TestMethod]
    [DataRow(TelemetryConsentState.Unknown)]
    [DataRow(TelemetryConsentState.Denied)]
    public void TrackEvent_WithoutConsent_ShouldDiscardEvent(TelemetryConsentState state)
    {
        TelemetryConsentService consent = new();
        consent.SetState(state);
        RecordingTelemetryEventSink sink = new();
        ConsentAwareTelemetryService telemetry = new(consent, sink);

        telemetry.TrackEvent("capture.completed");

        Assert.IsEmpty(sink.Events);
    }

    [TestMethod]
    public void TrackEvent_WithConsent_ShouldForwardEventUnchanged()
    {
        TelemetryConsentService consent = new();
        consent.SetState(TelemetryConsentState.Granted);
        RecordingTelemetryEventSink sink = new();
        ConsentAwareTelemetryService telemetry = new(consent, sink);
        IReadOnlyDictionary<string, object?> properties =
            new Dictionary<string, object?> { ["media_type"] = "video" };

        telemetry.TrackEvent("capture.completed", properties);

        Assert.HasCount(1, sink.Events);
        Assert.AreEqual("capture.completed", sink.Events[0].Name);
        Assert.AreSame(properties, sink.Events[0].Properties);
    }

    [TestMethod]
    public void TrackEvent_AfterConsentIsRevoked_ShouldDiscardSubsequentEvents()
    {
        TelemetryConsentService consent = new();
        consent.SetState(TelemetryConsentState.Granted);
        RecordingTelemetryEventSink sink = new();
        ConsentAwareTelemetryService telemetry = new(consent, sink);

        telemetry.TrackEvent("first");
        consent.SetState(TelemetryConsentState.Denied);
        telemetry.TrackEvent("second");

        Assert.HasCount(1, sink.Events);
        Assert.AreEqual("first", sink.Events[0].Name);
    }

    private sealed class RecordingTelemetryEventSink : ITelemetryEventSink
    {
        public List<(string Name, IReadOnlyDictionary<string, object?>? Properties)> Events { get; } = [];

        public void TrackEvent(
            string eventName,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add((eventName, properties));
        }
    }
}
