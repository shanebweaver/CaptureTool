using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Tests;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class AudioCaptureWorkflowTests
{
    [TestMethod]
    public void StartCapture_WhenStopped_StartsRecorderAndRaisesStateChanged()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        AudioCaptureState? raisedState = null;
        workflow.CaptureStateChanged += (_, state) => raisedState = state;

        workflow.StartCapture();

        workflow.IsRecording.Should().BeTrue();
        workflow.CaptureState.Should().Be(AudioCaptureState.Recording);
        raisedState.Should().Be(AudioCaptureState.Recording);
        recorder.Verify(service => service.StartCapture(It.Is<string>(path => path.EndsWith(".wav"))), Times.Once);
    }

    [TestMethod]
    public void StartCapture_WhenAlreadyRecording_Throws()
    {
        AudioCaptureWorkflow workflow = CreateWorkflow(new Mock<IAudioRecorder>());

        workflow.StartCapture();

        workflow.Invoking(service => service.StartCapture()).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void StartCapture_WhenRecorderFails_ClearsSession()
    {
        var recorder = new Mock<IAudioRecorder>();
        recorder
            .Setup(service => service.StartCapture(It.IsAny<string>()))
            .Throws(new InvalidOperationException("failed"));
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);

        workflow.Invoking(service => service.StartCapture()).Should().Throw<InvalidOperationException>();

        workflow.IsRecording.Should().BeFalse();
        workflow.CaptureState.Should().Be(AudioCaptureState.Stopped);
    }

    [TestMethod]
    public void StartCapture_AppliesDefaultDesktopAudioSetting()
    {
        var recorder = new Mock<IAudioRecorder>();
        Mock<ISettingsService> settings = CreateSettings(defaultDesktopAudioEnabled: false);
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder, settings: settings);

        workflow.StartCapture();

        workflow.IsDesktopAudioEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void PauseCapture_WhenRecording_TogglesPauseAndResume()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        List<AudioCaptureState> raisedStates = [];
        workflow.CaptureStateChanged += (_, state) => raisedStates.Add(state);

        workflow.StartCapture();
        workflow.PauseCapture();
        workflow.PauseCapture();

        workflow.IsPaused.Should().BeFalse();
        raisedStates.Should().Equal(AudioCaptureState.Recording, AudioCaptureState.Paused, AudioCaptureState.Recording);
        recorder.Verify(service => service.Pause(), Times.Once);
        recorder.Verify(service => service.Resume(), Times.Once);
    }

    [TestMethod]
    public void PauseCapture_WhenNotRecording_Throws()
    {
        AudioCaptureWorkflow workflow = CreateWorkflow(new Mock<IAudioRecorder>());

        workflow.Invoking(service => service.PauseCapture()).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void StopCapture_WhenRecording_StopsRecorderReturnsFileAndRaisesCapturedEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        var audioFile = new AudioFile(@"C:\Temp\capture.wav");
        recorder.Setup(service => service.StopCapture()).Returns(audioFile);
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        AudioFile? raisedFile = null;
        workflow.NewAudioCaptured += (_, file) => raisedFile = file;
        workflow.StartCapture();

        AudioFile stoppedFile = workflow.StopCapture();

        workflow.CaptureState.Should().Be(AudioCaptureState.Stopped);
        stoppedFile.Should().BeSameAs(audioFile);
        raisedFile.Should().BeSameAs(audioFile);
        recorder.Verify(service => service.StopCapture(), Times.Once);
    }

    [TestMethod]
    public async Task StopCapture_WhenAutoSaveAndCopyEnabled_PostProcessesCompletedAudio()
    {
        var recorder = new Mock<IAudioRecorder>();
        var audioFile = new AudioFile(@"C:\Temp\capture.wav");
        recorder.Setup(service => service.StopCapture()).Returns(audioFile);
        Mock<ISettingsService> settings = CreateSettings(autoSave: true, autoCopy: true, audioFolder: @"C:\Audio");
        var fileSystem = new Mock<IFileSystem>();
        var clipboard = new Mock<IClipboardService>();
        TaskCompletionSource<object?> copied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        clipboard
            .Setup(service => service.CopyFileAsync(It.Is<ClipboardFile>(file => file.FilePath == audioFile.FilePath)))
            .Callback(() => copied.TrySetResult(null))
            .Returns(Task.CompletedTask);
        AudioCaptureWorkflow workflow = CreateWorkflow(
            recorder,
            settings: settings,
            fileSystem: fileSystem,
            clipboard: clipboard);

        workflow.StartCapture();
        workflow.StopCapture();

        fileSystem.Verify(
            service => service.CopyFile(
                audioFile.FilePath,
                It.Is<string>(path => path.StartsWith(@"C:\Audio", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".wav")),
                true),
            Times.Once);
        await copied.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    [TestMethod]
    public void AudioLevelCaptured_WhenRecording_ForwardsLevel()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        var level = new AudioCaptureLevel(.5, .25, 42);
        AudioCaptureLevel? raisedLevel = null;
        workflow.AudioLevelCaptured += (_, value) => raisedLevel = value;
        workflow.StartCapture();

        recorder.Raise(service => service.AudioLevelCaptured += null, recorder.Object, level);

        raisedLevel.Should().Be(level);
    }

    [TestMethod]
    public void StopCapture_WhenNotRecording_Throws()
    {
        AudioCaptureWorkflow workflow = CreateWorkflow(new Mock<IAudioRecorder>());

        workflow.Invoking(service => service.StopCapture()).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void ToggleMute_TogglesMutedStateAndRaisesEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        bool? raisedValue = null;
        workflow.MutedStateChanged += (_, value) => raisedValue = value;

        workflow.ToggleMute();

        workflow.IsMuted.Should().BeTrue();
        raisedValue.Should().BeTrue();
        recorder.Verify(service => service.ToggleMute(), Times.Once);
    }

    [TestMethod]
    public void ToggleLocalAudio_TogglesDesktopAudioStateAndRaisesEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);
        bool? raisedValue = null;
        workflow.DesktopAudioStateChanged += (_, value) => raisedValue = value;

        workflow.IsDesktopAudioEnabled.Should().BeTrue();

        workflow.ToggleLocalAudio();

        workflow.IsDesktopAudioEnabled.Should().BeFalse();
        raisedValue.Should().BeFalse();
        recorder.Verify(service => service.ToggleDesktopAudio(), Times.Once);
    }

    [TestMethod]
    public void SelectAudioInputSource_ShouldNormalizeSourceAndUpdateRecorder()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioCaptureWorkflow workflow = CreateWorkflow(recorder);

        workflow.SelectAudioInputSource("microphone");
        workflow.SelectAudioInputSource(" ");

        workflow.SelectedAudioInputSourceId.Should().BeNull();
        recorder.Verify(service => service.SetAudioInputSource("microphone"), Times.Once);
        recorder.Verify(service => service.SetAudioInputSource(null), Times.Once);
    }

    private static AudioCaptureWorkflow CreateWorkflow(
        Mock<IAudioRecorder> recorder,
        Mock<ISettingsService>? settings = null,
        Mock<IStorageService>? storage = null,
        Mock<IFileSystem>? fileSystem = null,
        Mock<IClipboardService>? clipboard = null)
    {
        settings ??= CreateSettings();

        storage ??= new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(@"C:\Temp");
        storage.Setup(service => service.GetSystemDefaultMusicFolderPath()).Returns(@"C:\Music");

        var taskEnvironment = new Mock<ITaskEnvironment>();
        taskEnvironment
            .Setup(environment => environment.TryExecute(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(true);

        AudioCaptureFileNameGenerator fileNameGenerator = new(TestClock.Instance);
        AudioCapturePostProcessor postProcessor = new(
            clipboard?.Object ?? Mock.Of<IClipboardService>(),
            fileSystem?.Object ?? Mock.Of<IFileSystem>(),
            settings.Object,
            storage.Object,
            taskEnvironment.Object,
            Mock.Of<ITelemetryService>(),
            fileNameGenerator);

        return new AudioCaptureWorkflow(
            recorder.Object,
            settings.Object,
            storage.Object,
            new AudioCaptureStateStore(),
            postProcessor,
            fileNameGenerator);
    }

    private static Mock<ISettingsService> CreateSettings(
        bool defaultDesktopAudioEnabled = true,
        bool autoSave = false,
        bool autoCopy = false,
        string audioFolder = "")
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled))
            .Returns(defaultDesktopAudioEnabled);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_AudioCapture_AutoSave))
            .Returns(autoSave);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_AudioCapture_AutoCopy))
            .Returns(autoCopy);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder))
            .Returns(audioFolder);
        return settings;
    }
}
