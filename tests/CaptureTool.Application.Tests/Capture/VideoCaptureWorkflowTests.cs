using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using FluentAssertions;
using Moq;
using System.Drawing;

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

        context.Workflow.StartVideoCapture(CreateCaptureArgs());

        context.Workflow.IsRecording.Should().BeTrue();
        context.ScreenRecorder.Verify(recorder => recorder.StartRecording(It.Is<CaptureRecordingOptions>(options =>
            options.CaptureAudio &&
            options.AudioInputSourceId == "microphone-id" &&
            options.AudioInputVolumePercentage == 42 &&
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
        TestWorkflowContext context = CreateContext();
        List<bool> raisedStates = [];
        context.Workflow.PausedStateChanged += (_, state) => raisedStates.Add(state);

        context.Workflow.StartVideoCapture(CreateCaptureArgs());
        context.Workflow.ToggleIsPaused(true);
        context.Workflow.CancelVideoCapture();

        context.Workflow.IsRecording.Should().BeFalse();
        context.ScreenRecorder.Verify(recorder => recorder.StopRecording(), Times.Once);
        raisedStates.Should().Equal(true, false);
    }

    [TestMethod]
    public void AudioChanges_ShouldUpdateRecorder_WhenRecording()
    {
        TestWorkflowContext context = CreateContext();
        context.Workflow.StartVideoCapture(CreateCaptureArgs());

        context.Workflow.SelectAudioInputSource("microphone-id");
        context.Workflow.SetIsAudioInputMuted(true);
        context.Workflow.SetAudioInputVolume(37);

        context.Workflow.SelectedAudioInputSourceId.Should().Be("microphone-id");
        context.Workflow.IsAudioInputMuted.Should().BeTrue();
        context.Workflow.AudioInputVolumePercentage.Should().Be(37);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputSource("microphone-id"), Times.Once);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputSource(null), Times.Once);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioCaptureEnabled(It.IsAny<bool>()), Times.Never);
        context.ScreenRecorder.Verify(recorder => recorder.SetAudioInputVolume(37), Times.Once);
    }

    private static TestWorkflowContext CreateContext(
        bool defaultDesktopAudioEnabled = true,
        bool runBackgroundTasksImmediately = true)
    {
        var screenRecorder = new Mock<IScreenRecorder>();
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
            .Setup(service => service.GetApplicationTemporaryFolderPath())
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

        var fileNameGenerator = new VideoCaptureFileNameGenerator(TestClock.Instance);
        var postProcessor = new VideoCapturePostProcessor(
            Mock.Of<IClipboardService>(),
            Mock.Of<IFileSystem>(),
            settings.Object,
            storage.Object,
            taskEnvironment.Object,
            Mock.Of<ILogService>(),
            fileNameGenerator);

        var workflow = new VideoCaptureWorkflow(
            screenRecorder.Object,
            settings.Object,
            storage.Object,
            backgroundTaskRunner.Object,
            new VideoCaptureStateStore(),
            postProcessor,
            fileNameGenerator);

        return new TestWorkflowContext(workflow, screenRecorder, backgroundTaskRunner, () => finalizeAction);
    }

    private static NewCaptureArgs CreateCaptureArgs()
    {
        MonitorCaptureResult monitor = new(
            IntPtr.Zero,
            [],
            96,
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080),
            true);

        return new NewCaptureArgs(monitor, new Rectangle(0, 0, 1920, 1080));
    }

    private sealed class TestWorkflowContext(
        VideoCaptureWorkflow workflow,
        Mock<IScreenRecorder> screenRecorder,
        Mock<IBackgroundTaskRunner> backgroundTaskRunner,
        Func<Action?> getFinalizeAction)
    {
        public VideoCaptureWorkflow Workflow { get; } = workflow;
        public Mock<IScreenRecorder> ScreenRecorder { get; } = screenRecorder;
        public Mock<IBackgroundTaskRunner> BackgroundTaskRunner { get; } = backgroundTaskRunner;
        public Action? FinalizeAction => getFinalizeAction();
    }
}
