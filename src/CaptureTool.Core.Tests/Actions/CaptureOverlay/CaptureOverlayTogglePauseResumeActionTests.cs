using AutoFixture;
using AutoFixture.AutoMoq;
using CaptureTool.Core.Implementations.Actions.CaptureOverlay;
using CaptureTool.Domains.Capture.Interfaces;
using FluentAssertions;
using Moq;

namespace CaptureTool.Core.Tests.Actions.CaptureOverlay;

[TestClass]
public class CaptureOverlayTogglePauseResumeActionTests
{
    private IFixture Fixture { get; set; } = null!;

    [TestInitialize]
    public void Init()
    {
        Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
    }

    [TestMethod]
    public void Execute_ShouldToggleToPaused_WhenNotPaused()
    {
        var handler = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        handler.SetupGet(h => h.IsPaused).Returns(false);
        handler.SetupGet(h => h.IsRecording).Returns(true);

        var action = Fixture.Create<CaptureOverlayTogglePauseResumeAction>();
        action.Execute();

        handler.Verify(h => h.ToggleIsPaused(true), Times.Once);
    }

    [TestMethod]
    public void Execute_ShouldToggleToResumed_WhenPaused()
    {
        var handler = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        handler.SetupGet(h => h.IsPaused).Returns(true);
        handler.SetupGet(h => h.IsRecording).Returns(true);

        var action = Fixture.Create<CaptureOverlayTogglePauseResumeAction>();
        action.Execute();

        handler.Verify(h => h.ToggleIsPaused(false), Times.Once);
    }

    [TestMethod]
    public void CanExecute_ShouldReturnTrue_WhenRecording()
    {
        var handler = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        handler.SetupGet(h => h.IsRecording).Returns(true);

        var action = Fixture.Create<CaptureOverlayTogglePauseResumeAction>();
        bool result = action.CanExecute();

        result.Should().BeTrue();
    }

    [TestMethod]
    public void CanExecute_ShouldReturnFalse_WhenNotRecording()
    {
        var handler = Fixture.Freeze<Mock<IVideoCaptureHandler>>();
        handler.SetupGet(h => h.IsRecording).Returns(false);

        var action = Fixture.Create<CaptureOverlayTogglePauseResumeAction>();
        bool result = action.CanExecute();

        result.Should().BeFalse();
    }
}
