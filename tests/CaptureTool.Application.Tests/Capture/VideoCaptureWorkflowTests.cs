using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.Capture;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class VideoCaptureWorkflowTests
{
    [TestMethod]
    public void PrepareForVideoCapture_ShouldApplyDefaultDesktopAudioSetting()
    {
        TestWorkflowContext context = CreateContext(defaultDesktopAudioEnabled: false);

        context.Workflow.PrepareForVideoCapture();

        context.Workflow.IsDesktopAudioEnabled.Should().BeFalse();
        context.Workflow.DesktopAudioVolumePercentage.Should().Be(100);
        context.Workflow.IsAudioInputMuted.Should().BeFalse();
        context.Workflow.AudioInputVolumePercentage.Should().Be(100);
    }

    [TestMethod]
    public void StartVideoCapture_ShouldStartRecorderWithCurrentAudioSettings()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.PrepareForVideoCapture();
        context.Workflow.SelectAudioInputSource("microphone-id");
        context.Workflow.SetAudioInputVolume(42);
        context.Workflow.SetDesktopAudioVolume(64);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());

        context.Workflow.IsRecording.Should().BeTrue();
        context.ScreenRecorder.Verify(recorder => recorder.StartRecording(It.Is<CaptureRecordingOptions>(options =>
            options.CaptureAudio &&
            options.AudioInputSourceId == "microphone-id" &&
            options.AudioInputVolumePercentage == 42 &&
            options.DesktopAudioVolumePercentage == 64 &&
            options.OutputPath.EndsWith(".mp4"))), Times.Once);
    }

    [TestMethod]
    public void StartVideoCapture_ShouldReturnToIdle_WhenRecorderStartFails()
    {
        TestWorkflowContext context = CreateContext();
        context.ScreenRecorder
            .Setup(recorder => recorder.StartRecording(It.IsAny<CaptureRecordingOptions>()))
            .Throws(new InvalidOperationException("Recorder failed."));

        Action act = () => context.Workflow.StartVideoCapture(CreateCaptureArgs());

        act.Should().Throw<InvalidOperationException>();
        context.Workflow.IsRecording.Should().BeFalse();
        context.Workflow.IsFinalizing.Should().BeFalse();
        context.FileSystem.Verify(
            service => service.DeleteFile(It.Is<string>(path => path.EndsWith(".mp4", StringComparison.Ordinal))),
            Times.Once);
    }

    [TestMethod]
    public void StartVideoCapture_ShouldTrackBoundedFailureStageAndReason_WhenRecorderStartFails()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);
        context.Workflow.PrepareForVideoCapture();
        context.ScreenRecorder
            .Setup(recorder => recorder.StartRecording(It.IsAny<CaptureRecordingOptions>()))
            .Throws(new InvalidOperationException(
                "Recorder failed with private details.",
                new VideoCaptureNotSupportedException(VideoCaptureUnsupportedReason.GraphicsCapture)));

        Action act = () => context.Workflow.StartVideoCapture(CreateCaptureArgs());

        act.Should().Throw<InvalidOperationException>();
        var failedEvent = events.Single(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
        failedEvent.Properties[TelemetryProperties.CaptureType].Should().Be(nameof(CaptureType.Rectangle));
        failedEvent.Properties[TelemetryProperties.DesktopAudioEnabled].Should().Be(true);
        failedEvent.Properties[TelemetryProperties.AudioInputEnabled].Should().Be(false);
        failedEvent.Properties[TelemetryProperties.FailureStage].Should().Be(TelemetryFailureStages.RecorderStart);
        failedEvent.Properties[TelemetryProperties.FailureReason].Should().Be(TelemetryFailureReasons.GraphicsUnsupported);
        failedEvent.Properties.Values
            .OfType<string>()
            .Should().NotContain(text =>
                text.Contains("private", StringComparison.OrdinalIgnoreCase) || text.Contains("1234"));
    }

    [TestMethod]
    public void StartVideoCapture_ShouldClassifyNativeCodecUnavailableHResult()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);
        context.ScreenRecorder
            .Setup(recorder => recorder.StartRecording(It.IsAny<CaptureRecordingOptions>()))
            .Throws(new ExternalException(
                "Native codec unavailable.",
                unchecked((int)0xC00D5212)));

        Action act = () => context.Workflow.StartVideoCapture(CreateCaptureArgs());

        act.Should().Throw<ExternalException>();
        var failedEvent = events.Single(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
        failedEvent.Properties[TelemetryProperties.FailureReason]
            .Should().Be(TelemetryFailureReasons.ComponentUnavailable);
    }

    [TestMethod]
    public void StartVideoCapture_ShouldTrackTargetUnavailable_WhenSelectedTargetIsInvalidated()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);
        context.ScreenRecorder
            .Setup(recorder => recorder.StartRecording(It.IsAny<CaptureRecordingOptions>()))
            .Throws(new VideoCaptureTargetUnavailableException());

        Action act = () => context.Workflow.StartVideoCapture(CreateCaptureArgs());

        act.Should().Throw<VideoCaptureTargetUnavailableException>();
        var failedEvent = events.Single(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
        failedEvent.Properties[TelemetryProperties.FailureStage].Should().Be(TelemetryFailureStages.RecorderStart);
        failedEvent.Properties[TelemetryProperties.FailureReason].Should().Be(TelemetryFailureReasons.TargetUnavailable);
    }

    [TestMethod]
    public void StartVideoCapture_WhenDeviceIsUnsupported_ShouldTrackAndRejectBeforeRecorder()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        var support = new Mock<IVideoCaptureSupportService>();
        support
            .Setup(service => service.GetSupportStatus())
            .Returns(VideoCaptureSupportStatus.Unsupported(VideoCaptureUnsupportedReason.OperatingSystem));
        TestWorkflowContext context = CreateContext(
            telemetryService: telemetry.Object,
            supportService: support.Object);

        Action act = () => context.Workflow.StartVideoCapture(CreateCaptureArgs(CaptureType.FullScreen));

        act.Should().Throw<VideoCaptureNotSupportedException>()
            .Which.Reason.Should().Be(VideoCaptureUnsupportedReason.OperatingSystem);
        context.ScreenRecorder.Verify(
            recorder => recorder.StartRecording(It.IsAny<CaptureRecordingOptions>()),
            Times.Never);
        events.Should().ContainSingle(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureRequested);
        var failedEvent = events.Single(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
        failedEvent.Properties[TelemetryProperties.CaptureType].Should().Be(nameof(CaptureType.FullScreen));
        failedEvent.Properties[TelemetryProperties.FailureStage].Should().Be(TelemetryFailureStages.RecorderStart);
        failedEvent.Properties[TelemetryProperties.FailureReason].Should().Be(TelemetryFailureReasons.PlatformUnsupported);
    }

    [TestMethod]
    public void CaptureTelemetry_ShouldPreserveRequestedCaptureTypeAcrossLifecycle()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);

        context.Workflow.StartVideoCapture(CreateCaptureArgs(CaptureType.FullScreen));
        context.ScreenRecorder.Raise(recorder => recorder.RecordingStarted += null, EventArgs.Empty);
        context.Workflow.CancelVideoCapture();

        events
            .Where(trackedEvent => trackedEvent.Name is
                TelemetryEvents.CaptureRequested or
                TelemetryEvents.CaptureStarted or
                TelemetryEvents.CaptureCanceled)
            .Should().OnlyContain(trackedEvent =>
                Equals(
                    trackedEvent.Properties[TelemetryProperties.CaptureType],
                    nameof(CaptureType.FullScreen)));
    }

    [TestMethod]
    public void RecordingStarted_ShouldRaiseOncePerSession()
    {
        TestWorkflowContext context = CreateContext();
        int recordingStartedCount = 0;
        context.Workflow.RecordingStarted += (_, _) => recordingStartedCount++;

        context.Workflow.StartVideoCapture(CreateCaptureArgs());

        context.ScreenRecorder.Raise(recorder => recorder.RecordingStarted += null, EventArgs.Empty);
        context.ScreenRecorder.Raise(recorder => recorder.RecordingStarted += null, EventArgs.Empty);

        recordingStartedCount.Should().Be(1);
    }

    [TestMethod]
    public void ToggleIsPaused_ShouldPauseAndResumeRecorder()
    {
        TestWorkflowContext context = CreateContext();
        List<bool> raisedStates = [];
        context.Workflow.PausedStateChanged += (_, state) => raisedStates.Add(state);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.Workflow.ToggleIsPaused(true);
        context.Workflow.ToggleIsPaused(false);

        context.ScreenRecorder.Verify(recorder => recorder.PauseRecording(), Times.Once);
        context.ScreenRecorder.Verify(recorder => recorder.ResumeRecording(), Times.Once);
        raisedStates.Should().Equal(true, false);
        context.Workflow.IsPaused.Should().BeFalse();
    }

    [TestMethod]
    public void StopVideoCapture_ShouldQueueFinalizationAndPublishPendingVideo()
    {
        TestWorkflowContext context = CreateContext(runBackgroundTasksImmediately: false);
        VideoFile? capturedVideo = null;
        context.Workflow.NewVideoCaptured += (_, video) => capturedVideo = video;

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        PendingVideoFile pendingVideo = context.Workflow.StopVideoCapture();

        context.Workflow.IsFinalizing.Should().BeTrue();
        capturedVideo.Should().BeSameAs(pendingVideo);
        context.BackgroundTaskRunner.Verify(runner => runner.Run(
                It.IsAny<Action>(),
                "Failed to finalize video capture."),
            Times.Once);
        context.FinalizeAction.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Finalization_ShouldStopRecorderCompletePendingVideoAndClearSession()
    {
        TestWorkflowContext context = CreateContext(runBackgroundTasksImmediately: false);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        PendingVideoFile pendingVideo = context.Workflow.StopVideoCapture();
        context.FinalizeAction!();

        await pendingVideo.WhenReadyAsync();
        pendingVideo.IsReady.Should().BeTrue();
        context.Workflow.IsRecording.Should().BeFalse();
        context.Workflow.IsFinalizing.Should().BeFalse();
        context.ScreenRecorder.Verify(recorder => recorder.StopRecording(), Times.Once);
        context.Lifecycle.Finalizations.Should()
            .ContainSingle()
            .Which.Should().Be((pendingVideo.FilePath, CaptureFileType.Video));
    }

    [TestMethod]
    public async Task Finalization_WhenCaptureAssetFinalizationFails_ShouldKeepPendingVideoSuccessful()
    {
        var lifecycle = new RecordingCaptureAssetLifecycleService
        {
            FinalizationException = new InvalidOperationException("Capture asset lifecycle failed."),
        };
        TestWorkflowContext context = CreateContext(
            runBackgroundTasksImmediately: false,
            lifecycle: lifecycle);
        VideoFile? raisedVideo = null;
        context.Workflow.NewVideoCaptured += (_, video) => raisedVideo = video;

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        PendingVideoFile pendingVideo = context.Workflow.StopVideoCapture();

        raisedVideo.Should().BeSameAs(pendingVideo);
        context.Lifecycle.Finalizations.Should().BeEmpty();

        context.FinalizeAction!();
        await pendingVideo.WhenReadyAsync();

        pendingVideo.IsReady.Should().BeTrue();
        context.Workflow.IsFinalizing.Should().BeFalse();
        context.Lifecycle.Finalizations.Should().ContainSingle()
            .Which.Should().Be((pendingVideo.FilePath, CaptureFileType.Video));
    }

    [TestMethod]
    public async Task Finalization_ShouldFailPendingVideoAndClearSession_WhenRecorderStopFails()
    {
        TestWorkflowContext context = CreateContext(runBackgroundTasksImmediately: false);
        var exception = new InvalidOperationException("stop failed");
        context.ScreenRecorder
            .Setup(recorder => recorder.StopRecording())
            .Throws(exception);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        PendingVideoFile pendingVideo = context.Workflow.StopVideoCapture();

        Action act = () => context.FinalizeAction!();

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(exception);
        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(pendingVideo.WhenReadyAsync);
        actual.Should().BeSameAs(exception);
        context.Workflow.IsRecording.Should().BeFalse();
        context.Workflow.IsFinalizing.Should().BeFalse();
    }

    [TestMethod]
    public void CancelVideoCapture_ShouldStopRecorderClearSessionAndRaisePausedFalse()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);
        List<bool> raisedStates = [];
        context.Workflow.PausedStateChanged += (_, state) => raisedStates.Add(state);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.Workflow.ToggleIsPaused(true);
        context.Workflow.CancelVideoCapture();

        context.Workflow.IsRecording.Should().BeFalse();
        context.ScreenRecorder.Verify(recorder => recorder.StopRecording(), Times.Once);
        raisedStates.Should().Equal(true, false);
        events.Should().ContainSingle(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureCanceled);
        events.Should().NotContain(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
    }

    [TestMethod]
    public void CancelVideoCapture_WhenFirstFrameTimesOut_ShouldTrackFailedInsteadOfCanceled()
    {
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        var telemetry = new Mock<ITelemetryService>();
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        TestWorkflowContext context = CreateContext(telemetryService: telemetry.Object);
        context.Workflow.StartVideoCapture(CreateCaptureArgs());

        context.Workflow.CancelVideoCapture(CancelVideoCaptureReason.StartTimeout);

        context.Workflow.IsRecording.Should().BeFalse();
        context.ScreenRecorder.Verify(recorder => recorder.StopRecording(), Times.Once);
        events.Should().NotContain(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureCanceled);
        var failedEvent = events.Single(trackedEvent => trackedEvent.Name == TelemetryEvents.CaptureFailed);
        failedEvent.Properties[TelemetryProperties.Outcome].Should().Be(TelemetryOutcomes.Failed);
        failedEvent.Properties[TelemetryProperties.FailureStage].Should().Be(TelemetryFailureStages.FirstFrame);
        failedEvent.Properties[TelemetryProperties.FailureReason].Should().Be(TelemetryFailureReasons.StartTimeout);
    }

    [TestMethod]
    public void AudioChanges_ShouldUpdateRecorder_WhenRecording()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        List<string?> raisedSources = [];
        List<bool> raisedMutedStates = [];
        context.Workflow.AudioInputSourceChanged += (_, sourceId) => raisedSources.Add(sourceId);
        context.Workflow.AudioInputMutedStateChanged += (_, isMuted) => raisedMutedStates.Add(isMuted);

        context.Workflow.SelectAudioInputSource("microphone-id");
        context.Workflow.SetIsAudioInputMuted(true);
        context.Workflow.SetAudioInputVolume(37);

        context.Workflow.SelectedAudioInputSourceId.Should().Be("microphone-id");
        context.Workflow.IsAudioInputMuted.Should().BeTrue();
        context.Workflow.AudioInputVolumePercentage.Should().Be(37);
        raisedSources.Should().Equal("microphone-id");
        raisedMutedStates.Should().Equal(true);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputSource("microphone-id"), Times.Once);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputSource(null), Times.Once);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioCaptureEnabled(It.IsAny<bool>()), Times.Never);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputVolume(37), Times.Once);
    }

    [TestMethod]
    public void SetIsDesktopAudioEnabled_WhenRecording_UpdatesRecorderBeforeStateAndPublishesCommittedValue()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.PrepareForVideoCapture();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioCaptureEnabled(false))
            .Callback(() => context.Workflow.IsDesktopAudioEnabled.Should().BeTrue());
        bool? raisedValue = null;
        context.Workflow.DesktopAudioStateChanged += (_, value) => raisedValue = value;

        context.Workflow.SetIsDesktopAudioEnabled(false);

        context.Workflow.IsDesktopAudioEnabled.Should().BeFalse();
        raisedValue.Should().BeFalse();
    }

    [TestMethod]
    public void SetIsDesktopAudioEnabled_WhenRecordingStartedDisabled_CanEnableDuringCapture()
    {
        TestWorkflowContext context = CreateContext(defaultDesktopAudioEnabled: false);
        context.Workflow.PrepareForVideoCapture();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioCaptureEnabled(true))
            .Callback(() => context.Workflow.IsDesktopAudioEnabled.Should().BeFalse());
        bool? raisedValue = null;
        context.Workflow.DesktopAudioStateChanged += (_, value) => raisedValue = value;

        context.Workflow.SetIsDesktopAudioEnabled(true);

        context.ScreenRecorder.Verify(recorder => recorder.SetAudioCaptureEnabled(true), Times.Once);
        context.Workflow.IsDesktopAudioEnabled.Should().BeTrue();
        raisedValue.Should().BeTrue();
    }

    [TestMethod]
    public void SetDesktopAudioVolume_WhenRecording_UpdatesRecorderBeforeStateAndPublishesCommittedValue()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetDesktopAudioVolume(64))
            .Callback(() => context.Workflow.DesktopAudioVolumePercentage.Should().Be(100));
        int? raisedValue = null;
        context.Workflow.DesktopAudioVolumeChanged += (_, value) => raisedValue = value;

        context.Workflow.SetDesktopAudioVolume(64);

        context.Workflow.DesktopAudioVolumePercentage.Should().Be(64);
        raisedValue.Should().Be(64);
        context.ScreenRecorder.Verify(recorder => recorder.SetDesktopAudioVolume(64), Times.Once);
    }

    [TestMethod]
    public void AudioChanges_ShouldNotCallRecorder_WhenIdle()
    {
        TestWorkflowContext context = CreateContext();

        context.Workflow.SetIsDesktopAudioEnabled(false);
        context.Workflow.SetDesktopAudioVolume(64);
        context.Workflow.SelectAudioInputSource("microphone-id");
        context.Workflow.SetIsAudioInputMuted(true);
        context.Workflow.SetAudioInputVolume(37);

        context.ScreenRecorder.Verify(recorder => recorder.SetAudioCaptureEnabled(It.IsAny<bool>()), Times.Never);
        context.ScreenRecorder.Verify(recorder => recorder.SetDesktopAudioVolume(It.IsAny<int>()), Times.Never);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputSource(It.IsAny<string?>()), Times.Never);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputVolume(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void SetIsDesktopAudioEnabled_WhenRecorderRejectsChange_PreservesStateAndDoesNotRaiseEvent()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.PrepareForVideoCapture();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioCaptureEnabled(false))
            .Throws(new InvalidOperationException("Platform rejected desktop audio change."));
        bool eventRaised = false;
        context.Workflow.DesktopAudioStateChanged += (_, _) => eventRaised = true;

        context.Workflow.Invoking(workflow => workflow.SetIsDesktopAudioEnabled(false))
            .Should().Throw<InvalidOperationException>();

        context.Workflow.IsDesktopAudioEnabled.Should().BeTrue();
        eventRaised.Should().BeFalse();
    }

    [TestMethod]
    public void SetIsAudioInputMuted_WhenRecorderRejectsChange_PreservesStateAndDoesNotRaiseEvent()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.SelectAudioInputSource("microphone-id");
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioInputSource(null))
            .Throws(new InvalidOperationException("Platform rejected mute change."));
        bool eventRaised = false;
        context.Workflow.AudioInputMutedStateChanged += (_, _) => eventRaised = true;

        context.Workflow.Invoking(workflow => workflow.SetIsAudioInputMuted(true))
            .Should().Throw<InvalidOperationException>();

        context.Workflow.IsAudioInputMuted.Should().BeFalse();
        context.Workflow.SelectedAudioInputSourceId.Should().Be("microphone-id");
        eventRaised.Should().BeFalse();
    }

    [TestMethod]
    public void SelectAudioInputSource_WhenRecorderRejectsChange_PreservesStateAndDoesNotRaiseEvent()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.SelectAudioInputSource("original-microphone");
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioInputSource("new-microphone"))
            .Throws(new InvalidOperationException("Platform rejected source change."));
        bool eventRaised = false;
        context.Workflow.AudioInputSourceChanged += (_, _) => eventRaised = true;

        context.Workflow.Invoking(workflow => workflow.SelectAudioInputSource("new-microphone"))
            .Should().Throw<InvalidOperationException>();

        context.Workflow.SelectedAudioInputSourceId.Should().Be("original-microphone");
        eventRaised.Should().BeFalse();
    }

    [TestMethod]
    public void SetAudioInputVolume_WhenRecorderRejectsChange_PreservesState()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetAudioInputVolume(37))
            .Throws(new InvalidOperationException("Platform rejected volume change."));

        context.Workflow.Invoking(workflow => workflow.SetAudioInputVolume(37))
            .Should().Throw<InvalidOperationException>();

        context.Workflow.AudioInputVolumePercentage.Should().Be(100);
    }

    [TestMethod]
    public void SetDesktopAudioVolume_WhenRecorderRejectsChange_PreservesStateAndDoesNotRaiseEvent()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.ScreenRecorder
            .Setup(recorder => recorder.SetDesktopAudioVolume(64))
            .Throws(new InvalidOperationException("Platform rejected desktop volume change."));
        bool eventRaised = false;
        context.Workflow.DesktopAudioVolumeChanged += (_, _) => eventRaised = true;

        context.Workflow.Invoking(workflow => workflow.SetDesktopAudioVolume(64))
            .Should().Throw<InvalidOperationException>();

        context.Workflow.DesktopAudioVolumePercentage.Should().Be(100);
        eventRaised.Should().BeFalse();
    }

    private static TestWorkflowContext CreateContext(
        bool defaultDesktopAudioEnabled = true,
        bool runBackgroundTasksImmediately = true,
        ITelemetryService? telemetryService = null,
        IVideoCaptureSupportService? supportService = null,
        RecordingCaptureAssetLifecycleService? lifecycle = null)
    {
        var screenRecorder = new Mock<IScreenRecorder>();
        lifecycle ??= new RecordingCaptureAssetLifecycleService();
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled))
            .Returns(defaultDesktopAudioEnabled);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave))
            .Returns(false);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy))
            .Returns(false);

        var storage = new Mock<IStorageService>();
        storage
            .Setup(service => service.GetApplicationRetainedCaptureFolderPath())
            .Returns(@"C:\Temp");

        var backgroundTaskRunner = new Mock<IBackgroundTaskRunner>();
        Action? finalizeAction = null;
        backgroundTaskRunner
            .Setup(runner => runner.Run(It.IsAny<Action>(), It.IsAny<string>()))
            .Callback<Action, string>((action, _) =>
            {
                finalizeAction = action;
                if (runBackgroundTasksImmediately)
                {
                    action();
                }
            });

        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        if (supportService is null)
        {
            var supported = new Mock<IVideoCaptureSupportService>();
            supported
                .Setup(service => service.GetSupportStatus())
                .Returns(VideoCaptureSupportStatus.Supported);
            supportService = supported.Object;
        }

        var fileNameGenerator = new VideoCaptureFileNameGenerator(TestClock.Instance);
        var fileSystem = new Mock<IFileSystem>();
        var fileAllocator = new CaptureFileAllocator(fileSystem.Object);
        var postProcessor = new VideoCapturePostProcessor(
            Mock.Of<IClipboardService>(),
            fileAllocator,
            settings.Object,
            storage.Object,
            taskEnvironment.Object,
            Mock.Of<IMainWindowActivationService>(),
            Mock.Of<ILogService>(),
            fileNameGenerator,
            lifecycle);

        var workflow = new VideoCaptureWorkflow(
            screenRecorder.Object,
            settings.Object,
            storage.Object,
            fileAllocator,
            backgroundTaskRunner.Object,
            supportService,
            new VideoCaptureStateStore(),
            postProcessor,
            fileNameGenerator,
            telemetryService);

        return new TestWorkflowContext(
            workflow,
            screenRecorder,
            fileSystem,
            backgroundTaskRunner,
            lifecycle,
            () => finalizeAction);
    }

    private static NewCaptureArgs CreateCaptureArgs(CaptureType captureType = CaptureType.Rectangle)
    {
        MonitorCaptureResult monitor = new(
            IntPtr.Zero,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new NewCaptureArgs(
            monitor,
            new Rectangle(0, 0, 1920, 1080),
            captureType);
    }

    private sealed class TestWorkflowContext(
        VideoCaptureWorkflow workflow,
        Mock<IScreenRecorder> screenRecorder,
        Mock<IFileSystem> fileSystem,
        Mock<IBackgroundTaskRunner> backgroundTaskRunner,
        RecordingCaptureAssetLifecycleService lifecycle,
        Func<Action?> getFinalizeAction)
    {
        public VideoCaptureWorkflow Workflow { get; } = workflow;
        public Mock<IScreenRecorder> ScreenRecorder { get; } = screenRecorder;
        public Mock<IFileSystem> FileSystem { get; } = fileSystem;
        public Mock<IBackgroundTaskRunner> BackgroundTaskRunner { get; } = backgroundTaskRunner;
        public RecordingCaptureAssetLifecycleService Lifecycle { get; } = lifecycle;
        public Action? FinalizeAction => getFinalizeAction();
    }
}
