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
    [DataRow(
        TelemetryEvents.CaptureRequested,
        "ct1_capture_requested_video_rectangle_true_false")]
    [DataRow(
        TelemetryEvents.CaptureStarted,
        "ct1_capture_started_video_rectangle_true_false")]
    [DataRow(
        TelemetryEvents.CaptureCompleted,
        "ct1_capture_completed_video_rectangle_true_false_succeeded")]
    public void Format_CaptureFunnel_ShouldRetainCaptureAndAudioDimensions(
        string eventName,
        string expected)
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            eventName,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.CaptureType] = "rectangle",
                [TelemetryProperties.DesktopAudioEnabled] = true,
                [TelemetryProperties.AudioInputEnabled] = false,
                [TelemetryProperties.Outcome] = TelemetryOutcomes.Succeeded
            });

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Format_CaptureFailed_ShouldRetainBoundedStartupFailureDimensions()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.CaptureFailed,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.CaptureType] = "rectangle",
                [TelemetryProperties.DesktopAudioEnabled] = true,
                [TelemetryProperties.AudioInputEnabled] = false,
                [TelemetryProperties.Outcome] = TelemetryOutcomes.Failed,
                [TelemetryProperties.FailureStage] = TelemetryFailureStages.RecorderStart,
                [TelemetryProperties.FailureReason] = TelemetryFailureReasons.TargetUnavailable
            });

        Assert.AreEqual(
            "ct1_capture_failed_video_rectangle_true_false_failed_recorder_start_target_unavailable",
            result);
    }

    [TestMethod]
    public void Format_CaptureFailed_ShouldRejectUnreviewedFailureValues()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.CaptureFailed,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.Outcome] = TelemetryOutcomes.Failed,
                [TelemetryProperties.FailureStage] = TelemetryFailureStages.RecorderStart,
                [TelemetryProperties.FailureReason] = @"C:\private\capture.mp4: device 1234"
            });

        Assert.AreEqual("ct1_capture_failed_video_failed_recorder_start", result);
    }

    [TestMethod]
    public void Format_CaptureFailed_ShouldRetainFirstFrameTimeoutDimensions()
    {
        string result = PartnerCenterTelemetryEventNameFormatter.Format(
            TelemetryEvents.CaptureFailed,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.CaptureType] = "window",
                [TelemetryProperties.DesktopAudioEnabled] = false,
                [TelemetryProperties.AudioInputEnabled] = false,
                [TelemetryProperties.Outcome] = TelemetryOutcomes.Failed,
                [TelemetryProperties.FailureStage] = TelemetryFailureStages.FirstFrame,
                [TelemetryProperties.FailureReason] = TelemetryFailureReasons.StartTimeout
            });

        Assert.AreEqual(
            "ct1_capture_failed_video_window_false_false_failed_first_frame_start_timeout",
            result);
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
