using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Application.Implementations.UseCases.CaptureOverlay;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;
using FluentAssertions;
using Moq;

namespace CaptureTool.Application.Tests.UseCases.CaptureOverlay;

[TestClass]
public class CaptureOverlayUseCasesTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    private CaptureOverlayUseCases Create(out Mock<ICaptureOverlayCloseUseCase> close,
                                        out Mock<ICaptureOverlayGoBackUseCase> goBack,
                                        out Mock<ICaptureOverlayToggleDesktopAudioUseCase> toggleAudio,
                                        out Mock<ICaptureOverlayTogglePauseResumeUseCase> togglePauseResume,
                                        out Mock<ICaptureOverlayStartVideoCaptureUseCase> startVideo,
                                        out Mock<ICaptureOverlayStopVideoCaptureUseCase> stopVideo)
    {
        close = Fixture.Freeze<Mock<ICaptureOverlayCloseUseCase>>();
        goBack = Fixture.Freeze<Mock<ICaptureOverlayGoBackUseCase>>();
        toggleAudio = Fixture.Freeze<Mock<ICaptureOverlayToggleDesktopAudioUseCase>>();
        togglePauseResume = Fixture.Freeze<Mock<ICaptureOverlayTogglePauseResumeUseCase>>();
        startVideo = Fixture.Freeze<Mock<ICaptureOverlayStartVideoCaptureUseCase>>();
        stopVideo = Fixture.Freeze<Mock<ICaptureOverlayStopVideoCaptureUseCase>>();

        // default allow execution
        close.Setup(c => c.CanExecute()).Returns(true);
        goBack.Setup(c => c.CanExecute()).Returns(true);
        toggleAudio.Setup(c => c.CanExecute()).Returns(true);
        togglePauseResume.Setup(c => c.CanExecute()).Returns(true);
        stopVideo.Setup(c => c.CanExecute()).Returns(true);

        var actions = new CaptureOverlayUseCases(
            close.Object,
            goBack.Object,
            toggleAudio.Object,
            togglePauseResume.Object,
            startVideo.Object,
            stopVideo.Object);

        return actions;
    }

    [TestMethod]
    public void Close_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out var close, out _, out _, out _, out _, out _);

        // Act
        actions.Close();

        // Assert
        close.Verify(c => c.CanExecute(), Times.Once);
        close.Verify(c => c.Execute(), Times.Once);
    }

    [TestMethod]
    public void CanClose_ShouldReturnFromUnderlying()
    {
        // Arrange
        var actions = Create(out var close, out _, out _, out _, out _, out _);
        close.Setup(c => c.CanExecute()).Returns(false);

        // Act
        bool result = actions.CanClose();

        // Assert
        result.Should().BeFalse();
        close.Verify(c => c.CanExecute(), Times.Once);
    }

    [TestMethod]
    public void GoBack_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out _, out var goBack, out _, out _, out _, out _);

        // Act
        actions.GoBack();

        // Assert
        goBack.Verify(c => c.CanExecute(), Times.Once);
        goBack.Verify(c => c.Execute(), Times.Once);
    }

    [TestMethod]
    public void ToggleDesktopAudio_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out _, out _, out var toggle, out _, out _, out _);

        // Act
        actions.ToggleDesktopAudio();

        // Assert
        toggle.Verify(c => c.CanExecute(), Times.Once);
        toggle.Verify(c => c.Execute(), Times.Once);
    }

    //[TestMethod]
    //public void StartVideoCapture_ShouldInvokeUnderlyingAction_WithArgs_WhenCanExecute()
    //{
    //    // Arrange
    //    var actions = Create(out _, out _, out _, out var start, out _);
    //    var monitor = Fixture.Create<MonitorCaptureResult>();
    //    var area = Fixture.Create<System.Drawing.Rectangle>();
    //    var args = new NewCaptureArgs(monitor, area);
    //    start.Setup(s => s.CanExecute(args)).Returns(true);

    //    // Act
    //    actions.StartVideoCapture(args);

    //    // Assert
    //    start.Verify(s => s.CanExecute(args), Times.Once);
    //    start.Verify(s => s.Execute(args), Times.Once);
    //}

    //[TestMethod]
    //public void CanStartVideoCapture_ShouldReturnFromUnderlying()
    //{
    //    // Arrange
    //    var actions = Create(out _, out _, out _, out var start, out _);
    //    var monitor = Fixture.Create<MonitorCaptureResult>();
    //    var area = Fixture.Create<System.Drawing.Rectangle>();
    //    var args = new NewCaptureArgs(monitor, area);
    //    start.Setup(s => s.CanExecute(args)).Returns(false);

    //    // Act
    //    bool result = actions.CanStartVideoCapture(args);

    //    // Assert
    //    result.Should().BeFalse();
    //    start.Verify(s => s.CanExecute(args), Times.Once);
    //}

    [TestMethod]
    public void StopVideoCapture_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out _, out _, out _, out _, out _, out var stop);

        // Act
        actions.StopVideoCapture();

        // Assert
        stop.Verify(c => c.CanExecute(), Times.Once);
        stop.Verify(c => c.Execute(), Times.Once);
    }

    [TestMethod]
    public void CanStopVideoCapture_ShouldReturnFromUnderlying()
    {
        // Arrange
        var actions = Create(out _, out _, out _, out _, out _, out var stop);
        stop.Setup(s => s.CanExecute()).Returns(false);

        // Act
        bool result = actions.CanStopVideoCapture();

        // Assert
        result.Should().BeFalse();
        stop.Verify(s => s.CanExecute(), Times.Once);
    }

    [TestMethod]
    public void TogglePauseResume_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out _, out _, out _, out var togglePauseResume, out _, out _);

        // Act
        actions.TogglePauseResume();

        // Assert
        togglePauseResume.Verify(c => c.CanExecute(), Times.Once);
        togglePauseResume.Verify(c => c.Execute(), Times.Once);
    }

    [TestMethod]
    public void CanTogglePauseResume_ShouldReturnFromUnderlying()
    {
        // Arrange
        var actions = Create(out _, out _, out _, out var togglePauseResume, out _, out _);
        togglePauseResume.Setup(s => s.CanExecute()).Returns(false);

        // Act
        bool result = actions.CanTogglePauseResume();

        // Assert
        result.Should().BeFalse();
        togglePauseResume.Verify(s => s.CanExecute(), Times.Once);
    }
}
