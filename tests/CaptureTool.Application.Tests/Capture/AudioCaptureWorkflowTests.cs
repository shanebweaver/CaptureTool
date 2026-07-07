using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Tests.Features;
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

    private static AudioCaptureWorkflow CreateWorkflow(Mock<IAudioRecorder> recorder)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(@"C:\Temp");
        return new AudioCaptureWorkflow(
            recorder.Object,
            storage.Object,
            new AudioCaptureStateStore(),
            new AudioCaptureFileNameGenerator(TestClock.Instance));
    }
}
