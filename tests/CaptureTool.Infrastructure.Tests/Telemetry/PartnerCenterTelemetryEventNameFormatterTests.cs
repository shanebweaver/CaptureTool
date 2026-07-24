using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Telemetry;

[TestClass]
public sealed class PartnerCenterTelemetryEventNameFormatterTests
{
    [TestMethod]
    public void Format_WithoutProperties_ShouldUseSchemaPrefixAndEventName()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.AppStarted);

        Assert.AreEqual("ct1_app_started", result);
    }

    [TestMethod]
    public void Format_CaptureCompleted_ShouldRetainAggregateMediaAndOutcomeDimensions()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.CaptureCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.CaptureType] = "monitor",
                [TelemetryProperties.Outcome] = TelemetryOutcomes.Succeeded
            });

        Assert.AreEqual("ct1_capture_completed_video_succeeded", result);
    }

    [TestMethod]
    public void Format_UiCommandInvoked_ShouldCreateReadableButtonEventName()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.UiCommandInvoked,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Action] = "OpenSelectionOverlay",
                [TelemetryProperties.Surface] = "image_editor"
            });

        Assert.AreEqual(
            "ct1_ui_command_invoked_image_editor_open_selection_overlay",
            result);
    }

    [TestMethod]
    public void Format_ShouldIgnorePropertiesNotAllowListedForEvent()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.AppStarted,
            new Dictionary<string, object?>
            {
                ["unexpected"] = @"C:\private\capture.png"
            });

        Assert.AreEqual("ct1_app_started", result);
    }

    [TestMethod]
    public void Format_LongName_ShouldBeBoundedAndDeterministic()
    {
        string longEventName = new('a', 200);

        string first = PartnerCenterTelemetryEventNameFormatter.Format(longEventName);
        string second = PartnerCenterTelemetryEventNameFormatter.Format(longEventName);

        Assert.HasCount(96, first);
        Assert.AreEqual(first, second);
    }
}
