using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class AudioCaptureHandlerTests
{
    [TestMethod]
    public void StartCapture_WhenStopped_StartsRecorderAndRaisesStateChanged()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = CreateHandler(recorder);
        AudioCaptureState? raisedState = null;
        handler.CaptureStateChanged += (_, state) => raisedState = state;

        handler.StartCapture();

        Assert.IsTrue(handler.IsRecording);
        Assert.AreEqual(AudioCaptureState.Recording, raisedState);
        recorder.Verify(service => service.StartCapture(It.Is<string>(path => path.EndsWith(".wav"))), Times.Once);
    }

    [TestMethod]
    public void StartCapture_RaisesRecordingStarted_WhenFirstAudioSampleArrives()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioSampleCallback? audioSampleCallback = null;
        recorder
            .Setup(service => service.RegisterAudioSampleCallback(It.IsAny<AudioSampleCallback?>()))
            .Callback<AudioSampleCallback?>(callback => audioSampleCallback = callback);
        var handler = CreateHandler(recorder);
        int recordingStartedCount = 0;
        handler.RecordingStarted += (_, _) => recordingStartedCount++;

        handler.StartCapture();

        Assert.AreEqual(0, recordingStartedCount);
        Assert.IsNotNull(audioSampleCallback);

        AudioSampleData sample = new();
        audioSampleCallback!(ref sample);
        audioSampleCallback(ref sample);

        Assert.AreEqual(1, recordingStartedCount);
    }

    [TestMethod]
    public void StartCapture_RaisesPendingRecordingStarted_WhenFirstAudioSampleArrivesBeforeStateChanges()
    {
        var recorder = new Mock<IAudioRecorder>();
        AudioSampleCallback? audioSampleCallback = null;
        recorder
            .Setup(service => service.RegisterAudioSampleCallback(It.IsAny<AudioSampleCallback?>()))
            .Callback<AudioSampleCallback?>(callback => audioSampleCallback = callback);
        recorder
            .Setup(service => service.StartCapture(It.IsAny<string>()))
            .Callback(() =>
            {
                AudioSampleData sample = new();
                audioSampleCallback!(ref sample);
            });
        var handler = CreateHandler(recorder);
        int recordingStartedCount = 0;
        handler.RecordingStarted += (_, _) => recordingStartedCount++;

        handler.StartCapture();

        Assert.AreEqual(1, recordingStartedCount);
    }

    [TestMethod]
    public void StartCapture_WhenAlreadyRecording_Throws()
    {
        var handler = CreateHandler(new Mock<IAudioRecorder>());

        handler.StartCapture();

        Assert.ThrowsExactly<InvalidOperationException>(handler.StartCapture);
    }

    [TestMethod]
    public void PauseCapture_WhenRecording_PausesRecorderAndRaisesStateChanged()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = CreateHandler(recorder);
        handler.StartCapture();
        AudioCaptureState? raisedState = null;
        handler.CaptureStateChanged += (_, state) => raisedState = state;

        handler.PauseCapture();

        Assert.IsTrue(handler.IsPaused);
        Assert.AreEqual(AudioCaptureState.Paused, raisedState);
        recorder.Verify(service => service.Pause(), Times.Once);
    }

    [TestMethod]
    public void PauseCapture_WhenNotRecording_Throws()
    {
        var handler = CreateHandler(new Mock<IAudioRecorder>());

        Assert.ThrowsExactly<InvalidOperationException>(handler.PauseCapture);
    }

    [TestMethod]
    public void StopCapture_WhenNotRecording_Throws()
    {
        var handler = CreateHandler(new Mock<IAudioRecorder>());

        Assert.ThrowsExactly<InvalidOperationException>(() => handler.StopCapture());
    }

    [TestMethod]
    public void StopCapture_WhenRecording_StopsRecorderReturnsFileAndRaisesCapturedEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        var audioFile = new AudioFile(@"C:\Temp\capture.wav");
        recorder.Setup(service => service.StopCapture()).Returns(audioFile);
        var handler = CreateHandler(recorder);
        IAudioFile? raisedFile = null;
        handler.NewAudioCaptured += (_, file) => raisedFile = file;
        handler.StartCapture();

        IAudioFile stoppedFile = handler.StopCapture();

        Assert.AreEqual(AudioCaptureState.Stopped, handler.CaptureState);
        Assert.AreSame(audioFile, stoppedFile);
        Assert.AreSame(audioFile, raisedFile);
        recorder.Verify(service => service.StopCapture(), Times.Once);
    }

    [TestMethod]
    public void ToggleMute_TogglesMutedStateAndRaisesEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = CreateHandler(recorder);
        bool? raisedValue = null;
        handler.MutedStateChanged += (_, value) => raisedValue = value;

        handler.ToggleMute();

        Assert.IsTrue(handler.IsMuted);
        Assert.IsTrue(raisedValue);
        recorder.Verify(service => service.ToggleMute(), Times.Once);
    }

    [TestMethod]
    public void ToggleLocalAudio_TogglesDesktopAudioStateAndRaisesEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = CreateHandler(recorder);
        bool? raisedValue = null;
        handler.DesktopAudioStateChanged += (_, value) => raisedValue = value;

        Assert.IsTrue(handler.IsDesktopAudioEnabled);

        handler.ToggleLocalAudio();

        Assert.IsFalse(handler.IsDesktopAudioEnabled);
        Assert.IsFalse(raisedValue);
        recorder.Verify(service => service.ToggleDesktopAudio(), Times.Once);
    }

    private static AudioCaptureHandler CreateHandler(Mock<IAudioRecorder> recorder)
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(@"C:\Temp");
        return new AudioCaptureHandler(recorder.Object, storage.Object);
    }
}
