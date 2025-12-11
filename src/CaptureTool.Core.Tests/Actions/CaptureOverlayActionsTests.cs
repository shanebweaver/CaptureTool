using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Common.Commands;
using FluentAssertions;
using Moq;

namespace CaptureTool.Core.Tests.Actions;

[TestClass]
public class CaptureOverlayActionsTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    private CaptureOverlayActions Create(out Mock<ICaptureOverlayCloseAction> close,
                                        out Mock<ICaptureOverlayGoBackAction> goBack,
                                        out Mock<ICaptureOverlayToggleDesktopAudioAction> toggleAudio,
                                        out Mock<ICaptureOverlayStartVideoCaptureAction> startVideo,
                                        out Mock<ICaptureOverlayStopVideoCaptureAction> stopVideo)
    {
        close = Fixture.Freeze<Mock<ICaptureOverlayCloseAction>>();
        goBack = Fixture.Freeze<Mock<ICaptureOverlayGoBackAction>>();
        toggleAudio = Fixture.Freeze<Mock<ICaptureOverlayToggleDesktopAudioAction>>();
        startVideo = Fixture.Freeze<Mock<ICaptureOverlayStartVideoCaptureAction>>();
        stopVideo = Fixture.Freeze<Mock<ICaptureOverlayStopVideoCaptureAction>>();

        // default allow execution
        close.Setup(c => c.CanExecute()).Returns(true);
        goBack.Setup(c => c.CanExecute()).Returns(true);
        toggleAudio.Setup(c => c.CanExecute()).Returns(true);
        stopVideo.Setup(c => c.CanExecute()).Returns(true);

        var actions = new CaptureOverlayActions(
            close.Object,
            goBack.Object,
            toggleAudio.Object,
            startVideo.Object,
            stopVideo.Object);

        return actions;
    }

    [TestMethod]
    public void Close_ShouldInvokeUnderlyingAction_WhenCanExecute()
    {
        // Arrange
        var actions = Create(out var close, out _, out _, out _, out _);

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
        var actions = Create(out var close, out _, out _, out _, out _);
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
        var actions = Create(out _, out var goBack, out _, out _, out _);

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
        var actions = Create(out _, out _, out var toggle, out _, out _);

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
        var actions = Create(out _, out _, out _, out _, out var stop);

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
        var actions = Create(out _, out _, out _, out _, out var stop);
        stop.Setup(s => s.CanExecute()).Returns(false);

        // Act
        bool result = actions.CanStopVideoCapture();

        // Assert
        result.Should().BeFalse();
        stop.Verify(s => s.CanExecute(), Times.Once);
    }
}
