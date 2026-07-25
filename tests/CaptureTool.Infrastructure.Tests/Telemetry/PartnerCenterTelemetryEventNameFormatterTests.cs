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

    [TestMethod]
    public void Format_ShouldHandleEmptyBooleanAndNumericSegments()
    {
        string empty = PartnerCenterTelemetryEventNameFormatter.Format("---");
        string boolean = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Setting] = "enabled",
                [TelemetryProperties.Value] = true
            });
        string numeric = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Setting] = "count",
                [TelemetryProperties.Value] = 42
            });

        Assert.AreEqual("ct1", empty);
        Assert.AreEqual("ct1_settings_changed_enabled_true", boolean);
        Assert.AreEqual("ct1_settings_changed_count_42", numeric);
    }
}
