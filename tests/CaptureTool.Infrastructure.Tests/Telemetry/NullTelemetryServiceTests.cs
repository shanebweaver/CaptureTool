using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class NullTelemetryServiceTests
{
    [TestMethod]
    public void TrackEvent_ShouldDiscardEvent()
    {
        var telemetry = new NullTelemetryService();

        telemetry.TrackEvent(
            "capture.completed",
            new Dictionary<string, object?>
            {
                ["media_type"] = "image",
                ["outcome"] = "succeeded"
            });
    }
}
