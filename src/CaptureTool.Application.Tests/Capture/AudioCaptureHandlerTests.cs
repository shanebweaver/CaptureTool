using CaptureTool.Application.Features.AudioCapture;
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
        var handler = new AudioCaptureHandler(recorder.Object);
        AudioCaptureState? raisedState = null;
        handler.CaptureStateChanged += (_, state) => raisedState = state;

        handler.StartCapture();

        Assert.IsTrue(handler.IsRecording);
        Assert.AreEqual(AudioCaptureState.Recording, raisedState);
        recorder.Verify(service => service.StartCapture(), Times.Once);
    }

    [TestMethod]
    public void StartCapture_WhenAlreadyRecording_Throws()
    {
        var handler = new AudioCaptureHandler(Mock.Of<IAudioRecorder>());

        handler.StartCapture();

        Assert.ThrowsExactly<InvalidOperationException>(handler.StartCapture);
    }

    [TestMethod]
    public void PauseCapture_WhenRecording_PausesRecorderAndRaisesStateChanged()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = new AudioCaptureHandler(recorder.Object);
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
        var handler = new AudioCaptureHandler(Mock.Of<IAudioRecorder>());

        Assert.ThrowsExactly<InvalidOperationException>(handler.PauseCapture);
    }

    [TestMethod]
    public void StopCapture_WhenNotRecording_Throws()
    {
        var handler = new AudioCaptureHandler(Mock.Of<IAudioRecorder>());

        Assert.ThrowsExactly<InvalidOperationException>(() => handler.StopCapture());
    }

    [TestMethod]
    public void StopCapture_WhenRecording_StopsRecorderAndUpdatesStateBeforeNotImplementedException()
    {
        var recorder = new Mock<IAudioRecorder>();
        recorder.Setup(service => service.StopCapture()).Returns(Mock.Of<IAudioFile>());
        var handler = new AudioCaptureHandler(recorder.Object);
        handler.StartCapture();

        Assert.ThrowsExactly<NotImplementedException>(() => handler.StopCapture());

        Assert.AreEqual(AudioCaptureState.Stopped, handler.CaptureState);
        recorder.Verify(service => service.StopCapture(), Times.Once);
    }

    [TestMethod]
    public void ToggleMute_TogglesMutedStateAndRaisesEvent()
    {
        var recorder = new Mock<IAudioRecorder>();
        var handler = new AudioCaptureHandler(recorder.Object);
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
        var handler = new AudioCaptureHandler(recorder.Object);
        bool? raisedValue = null;
        handler.DesktopAudioStateChanged += (_, value) => raisedValue = value;

        handler.ToggleLocalAudio();

        Assert.IsTrue(handler.IsDesktopAudioEnabled);
        Assert.IsTrue(raisedValue);
        recorder.Verify(service => service.ToggleDesktopAudio(), Times.Once);
    }
}
